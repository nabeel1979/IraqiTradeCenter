using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IraqiTradeCenterCompany.API.Licensing;

/// <summary>
/// توقيع تسلسلي (Hash Chain) لسجلّات تفعيل الترخيص.
///
/// لكل صفّ في <c>licensing.LicenseActivations</c> نخزّن
/// <c>RowSig = HMAC-SHA256(AuthKey, Code|Days|StartDate|EndDate|AppliedAt|PrevSig)</c>
/// حيث <c>PrevSig</c> هو توقيع الصفّ السابق (الأقدم) أو <see cref="Genesis"/> لأوّل صفّ.
///
/// لماذا؟ لمنع التهكير المباشر لقاعدة البيانات:
///   • تعديل <c>EndDate</c> يدوياً → التوقيع المخزَّن لا يطابق المُعاد حسابه.
///   • إدراج صفّ تفعيل جديد بدون شفرة صالحة → لا يمكن توليد توقيع صحيح بدون <c>AuthKey</c>.
///   • حذف صفّ من المنتصف → السلسلة تنكسر (<c>PrevSig</c> لا يطابق).
///
/// كل هذا يعمل offline تماماً — لا يحتاج إنترنت ولا اتصال بقاعدة Parent.
/// </summary>
public static class LicenseChainSigner
{
    /// <summary>القيمة الأولية لسلسلة التواقيع (قبل أوّل صفّ تفعيل).</summary>
    public const string Genesis = "GENESIS";

    /// <summary>
    /// يحسب توقيع صفّ تفعيل بدلالة <c>AuthKey</c> وقيمه + توقيع الصفّ السابق.
    /// التواريخ تُكتب بصيغة ISO 8601 ثابتة (UTC) لضمان قابلية إعادة الإنتاج.
    /// </summary>
    public static string ComputeRowSig(
        string authKey,
        string code,
        int days,
        DateTime startDateUtc,
        DateTime endDateUtc,
        DateTime appliedAtUtc,
        string prevSig)
    {
        if (string.IsNullOrEmpty(authKey))
            throw new ArgumentException("AuthKey is required for signing.", nameof(authKey));

        var payload = string.Join('|',
            (code ?? "").ToUpperInvariant().Trim(),
            days.ToString(CultureInfo.InvariantCulture),
            ToIso(startDateUtc),
            ToIso(endDateUtc),
            ToIso(appliedAtUtc),
            prevSig ?? Genesis);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(authKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    /// <summary>تطبيع تاريخ إلى UTC ISO 8601 ثابت (يُحسم Kind إلى UTC إن لم يُحدَّد).</summary>
    private static string ToIso(DateTime dt)
    {
        var utc = dt.Kind switch
        {
            DateTimeKind.Utc         => dt,
            DateTimeKind.Local       => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => dt,
        };
        return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
    }
}
