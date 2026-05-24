using System.Security.Cryptography;
using System.Text;

namespace IraqiTradeCenterCompany.API.Licensing;

/// <summary>
/// شفرة الترخيص بصيغة <c>ITC-{CompanyKey}-{Days}-{ExpiryYYYYMMDD}-{Signature}</c>.
/// مثال: <c>ITC-C001-090-20260820-9F3D7A2C</c>.
///
/// التوقيع = أوّل 8 أحرف من SHA256(<c>{CompanyKey}|{Days}|{ExpiryYYYYMMDD}|{AuthKey}</c>)
/// بصيغة hex كبيرة. هذا ليس HMAC حقيقياً، لكنه قابل للحساب في T-SQL أيضاً
/// (<c>HASHBYTES('SHA2_256', ...)</c>) كي يتمكّن procedure في قاعدة Parent من
/// توليد شفرات بدون C#.
/// </summary>
public static class LicenseCode
{
    public const string Prefix = "ITC";

    /// <summary>توليد شفرة جديدة لشركة بمدّة محدّدة.</summary>
    public static string Generate(string companyKey, int days, DateTime expiryUtc, string authKey)
    {
        if (days <= 0) throw new ArgumentOutOfRangeException(nameof(days));
        var ck = NormalizeKey(companyKey);
        var d  = days.ToString("D3");
        var ex = expiryUtc.Date.ToString("yyyyMMdd");
        var sig = Sign(ck, d, ex, authKey);
        return $"{Prefix}-{ck}-{d}-{ex}-{sig}";
    }

    /// <summary>تحقّق + استخراج البيانات. يُرجع <c>true</c> لو الشفرة صالحة.</summary>
    public static bool TryParseAndVerify(
        string? raw, string companyKey, string authKey,
        out int days, out DateTime expiryUtc, out string? error)
    {
        days = 0; expiryUtc = default; error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "الشفرة فارغة";
            return false;
        }
        var s = raw.Trim().ToUpperInvariant();
        var parts = s.Split('-');
        if (parts.Length != 5 || parts[0] != Prefix)
        {
            error = "تنسيق الشفرة غير صحيح. الصيغة المتوقّعة: ITC-XXXX-NNN-YYYYMMDD-SSSSSSSS";
            return false;
        }
        var ck   = parts[1];
        var dStr = parts[2];
        var eStr = parts[3];
        var sig  = parts[4];

        if (!string.Equals(ck, NormalizeKey(companyKey), StringComparison.OrdinalIgnoreCase))
        {
            error = "هذه الشفرة لا تخصّ هذه الشركة (كود الشركة لا يطابق).";
            return false;
        }
        if (!int.TryParse(dStr, out days) || days <= 0 || days > 9999)
        {
            error = "عدد الأيام غير صحيح في الشفرة.";
            return false;
        }
        if (!DateTime.TryParseExact(eStr, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var exDate))
        {
            error = "تاريخ الانتهاء في الشفرة غير صالح.";
            return false;
        }
        expiryUtc = DateTime.SpecifyKind(exDate.Date, DateTimeKind.Utc);

        var expected = Sign(ck, dStr, eStr, authKey);
        if (!string.Equals(expected, sig, StringComparison.OrdinalIgnoreCase))
        {
            error = "توقيع الشفرة غير صحيح — الشفرة مزوَّرة أو لا تخصّ هذه الشركة.";
            return false;
        }

        // ‎تاريخ الانتهاء داخل الشفرة هو "آخر يوم يُمكن استخدام الشفرة فيه" (تاريخ
        // ‎الإصدار + سقف 365 يوم في العادة). بعد هذا التاريخ تُرفض الشفرة حتى لو
        // ‎كانت صحيحة التوقيع — هذا يمنع تكدّس الشفرات القديمة.
        if (expiryUtc.Date < DateTime.UtcNow.Date)
        {
            error = $"انتهت صلاحية هذه الشفرة بتاريخ {expiryUtc:yyyy-MM-dd} — يجب توليد شفرة جديدة.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// يحسب أوّل 8 أحرف Hex من SHA256(<c>{ck}|{d}|{e}|{authKey}</c>) — مطابق
    /// لما يفعله SQL Server: <c>UPPER(SUBSTRING(CONVERT(VARCHAR(64),
    /// HASHBYTES('SHA2_256', payload), 2), 1, 8))</c>.
    /// </summary>
    private static string Sign(string ck, string daysStr, string expiryStr, string authKey)
    {
        var payload = $"{ck}|{daysStr}|{expiryStr}|{authKey}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var sb = new StringBuilder(16);
        for (int i = 0; i < 4; i++) sb.Append(bytes[i].ToString("X2"));
        return sb.ToString();
    }

    /// <summary>يحوِّل أي مدخل لصيغة موحَّدة (uppercase, no spaces).</summary>
    public static string NormalizeKey(string s) => (s ?? "").Trim().ToUpperInvariant();
}
