using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;

/// <summary>
/// قواعد حماية حسابات الصناديق:
///   1. الحسابات المرتبطة بصندوق لا يجوز تحريكها عبر قيد عام (JV) — فقط عبر سندات
///      قبض/دفع المسجَّلة على نوع سند فيه VoucherTypeId.
///   2. السندات التي تحرّك صندوقاً يجب أن تحترم سقف المدين/الدائن المعرَّف لذلك
///      الصندوق بتلك العملة (إن وُجد).
///
/// هذه الفحوصات تُستدعى من جميع handlers الإدخال/التحديث: PostJournalEntry،
/// UpdateJournalEntry، UpdateVoucherEntry — لضمان عدم الالتفاف على الحماية من
/// أي مسار.
/// </summary>
internal static class CashBoxGuard
{
    /// <summary>تمثيل خفيف لسطر قيد (يقبل سطور موجودة أو سطور جديدة).</summary>
    public readonly record struct LineSnapshot(int AccountId, bool IsDebit, decimal Amount);

    /// <summary>
    /// يرجِع رسالة خطأ إذا كان هناك حساب صندوق في القيد دون VoucherTypeId،
    /// أو إذا تخطّى أحد السطور سقف الصندوق. يرجِع null إذا كان كل شيء سليماً.
    /// </summary>
    /// <param name="db">سياق قاعدة البيانات.</param>
    /// <param name="lines">سطور القيد الجديدة/المعدَّلة.</param>
    /// <param name="currency">عملة القيد.</param>
    /// <param name="voucherTypeId">معرّف نوع السند — null للقيد العام.</param>
    /// <param name="excludeJournalEntryId">عند التحديث: استثناء قيد قائم من حساب الرصيد الحالي.</param>
    public static async Task<string?> ValidateAsync(
        IAccountingDbContext db,
        IReadOnlyList<LineSnapshot> lines,
        string currency,
        int? voucherTypeId,
        int? excludeJournalEntryId,
        CancellationToken ct)
    {
        var lineAccountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        if (lineAccountIds.Count == 0) return null;

        var cashBoxes = await db.CashBoxes
            .AsNoTracking()
            .Include(c => c.Account)
            .Include(c => c.Currencies)
            .Where(c => lineAccountIds.Contains(c.AccountId))
            .ToListAsync(ct);

        if (cashBoxes.Count == 0) return null;

        // (1) لا يجوز تحريك حساب صندوق إلا عبر سند (VoucherTypeId مطلوب)
        if (!voucherTypeId.HasValue)
        {
            var firstBox = cashBoxes[0];
            return $"الحساب '{firstBox.Account?.NameAr ?? firstBox.NameAr}' مرتبط بصندوق ({firstBox.NameAr}) ولا يمكن تحريكه عبر قيد عام — استخدم سند قبض أو سند دفع.";
        }

        // (2) فحص السقوف لكل سطر يلامس صندوقاً
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();

        foreach (var box in cashBoxes)
        {
            // ‎مجموع تأثير السطور الحالية على هذا الصندوق
            decimal delta = 0m;
            foreach (var l in lines.Where(x => x.AccountId == box.AccountId))
            {
                delta += l.IsDebit ? l.Amount : -l.Amount;
            }

            // ‎الرصيد الحالي قبل هذا القيد (بنفس العملة)
            //   لا نعتمد على CurrentBalance المخزن — نحسب من الـ ledger لضمان الدقة
            //   مع استثناء القيد محل التحديث (لو موجود).
            //   نستخدم join صريحاً لأن JournalEntryLine لا يحوي navigation property
            //   عكسياً إلى JournalEntry في هذا الـ Domain.
            var ledger = from l in db.JournalEntryLines.AsNoTracking()
                         join e in db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
                         where l.AccountId == box.AccountId
                            && e.Currency == cur
                            && e.Status == JournalEntryStatus.Posted
                            && !l.IsDeleted
                            && !e.IsDeleted
                         select new { l.IsDebit, l.Amount, l.JournalEntryId };

            if (excludeJournalEntryId.HasValue)
            {
                var excludeId = excludeJournalEntryId.Value;
                ledger = ledger.Where(x => x.JournalEntryId != excludeId);
            }

            var currentDebit = await ledger.Where(x => x.IsDebit).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var currentCredit = await ledger.Where(x => !x.IsDebit).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var currentBalance = currentDebit - currentCredit;

            var newBalance = currentBalance + delta;

            // ‎السقوف المعرَّفة لهذا الصندوق بالعملة الحالية (إن وُجدت)
            var cbCur = box.Currencies.FirstOrDefault(c => c.IsActive &&
                string.Equals(c.Currency, cur, StringComparison.OrdinalIgnoreCase));

            if (cbCur == null)
            {
                return $"الصندوق '{box.NameAr}' لا يدعم العملة {cur} — أضف العملة إلى الصندوق أو غيّر عملة السند.";
            }

            // DebitLimit: أقصى رصيد مدين موجب (newBalance ≤ +DebitLimit)
            if (cbCur.DebitLimit.HasValue && newBalance > cbCur.DebitLimit.Value)
            {
                return $"تم تجاوز السقف المدين للصندوق '{box.NameAr}' ({cur}): الرصيد الناتج {Format(newBalance)} > السقف {Format(cbCur.DebitLimit.Value)}.";
            }

            // CreditLimit: أقصى رصيد دائن (newBalance ≥ -CreditLimit)
            if (cbCur.CreditLimit.HasValue && newBalance < -cbCur.CreditLimit.Value)
            {
                return $"تم تجاوز السقف الدائن للصندوق '{box.NameAr}' ({cur}): الرصيد الناتج {Format(newBalance)} < السقف -{Format(cbCur.CreditLimit.Value)}.";
            }
        }

        return null;
    }

    private static string Format(decimal v) =>
        v.ToString("N3", System.Globalization.CultureInfo.InvariantCulture);
}
