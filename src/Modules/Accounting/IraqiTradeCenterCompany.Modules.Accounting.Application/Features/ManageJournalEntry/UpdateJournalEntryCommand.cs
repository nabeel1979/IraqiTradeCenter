using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;

public record UpdateJournalEntryCommand(
    int Id,
    DateTime EntryDate,
    string Description,
    JournalEntryType EntryType,
    string Currency,
    List<UpdateJournalLine> Lines,
    bool PostImmediately = true,
    int? VoucherTypeId = null
) : IRequest<Result<int>>;

public record UpdateJournalLine(int AccountId, bool IsDebit, decimal Amount, string? Description);

public class UpdateJournalEntryHandler : IRequestHandler<UpdateJournalEntryCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService _currentUser;

    public UpdateJournalEntryHandler(IAccountingDbContext db,
        IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(UpdateJournalEntryCommand req, CancellationToken ct)
    {
        try
        {
            var entry = await _db.JournalEntries.Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
            if (entry == null) return Result.Failure<int>("القيد غير موجود");
            if (entry.Status == JournalEntryStatus.Reversed)
                return Result.Failure<int>("لا يمكن تعديل قيد معكوس");

            // ‎قفل قيود المناقلات بين الصناديق: لا تُعدَّل من هذه النافذة إطلاقاً —
            // ‎أيّ تعديل يجب أن يمرّ عبر نافذة المناقلات (إلغاء/تراجع عن استلام/تعديل).
            if (entry.ReferenceType == "CashBoxTransfer" || entry.ReferenceType == "CashBoxTransferReversal")
                return Result.Failure<int>(
                    "هذا القيد مولَّد من مناقلة بين صندوقَين — لا يمكن تعديله من نافذة القيود اليومية. " +
                    "افتح صفحة الصناديق ⇒ تبويب 'المناقلات' وقم بالتراجع عن الاستلام أو الإلغاء أولاً.");

            // ‎حارس السنة المالية النشطة:
            //   يُمنع تعديل قيد إذا كان تاريخه الأصلي (في قاعدة البيانات) خارج
            //   نطاق السنة المالية المُفَعَّلة. لا يُسمح بالالتفاف على ذلك بتغيير
            //   حقل التاريخ في الـ payload لأن المرجع هو القيمة المخزَّنة.
            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null)
            {
                var originalDate = entry.EntryDate.Date;
                if (originalDate < activeFy.StartDate.Date || originalDate > activeFy.EndDate.Date)
                {
                    return Result.Failure<int>(
                        $"تاريخ هذا القيد ({originalDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'. لتعديله، فعِّل السنة المالية المناسبة أولاً.");
                }
                // ‎إضافة: التاريخ الجديد المُرسَل من الواجهة يجب أن يكون كذلك ضمن السنة النشطة.
                var newDate = req.EntryDate.Date;
                if (newDate < activeFy.StartDate.Date || newDate > activeFy.EndDate.Date)
                {
                    return Result.Failure<int>(
                        $"التاريخ الجديد ({newDate:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'.");
                }
            }

            // منع التعديل من واجهة "القيود اليومية" إذا كان القيد مولّداً من سند مخصّص
            // غير مختلط (Debit/Credit) — يجب تعديله من نافذة السند المبسّطة.
            // أنواع السندات المختلطة (Mixed) تُحرَّر هنا مباشرةً بنفس واجهة القيود اليومية.
            if (entry.VoucherTypeId.HasValue)
            {
                var vtNature = await _db.JournalVoucherTypes.AsNoTracking()
                    .Where(v => v.Id == entry.VoucherTypeId.Value)
                    .Select(v => (Domain.Enums.VoucherNature?)v.Nature)
                    .FirstOrDefaultAsync(ct);
                if (vtNature != Domain.Enums.VoucherNature.Mixed)
                    return Result.Failure<int>("هذا القيد مولَّد من سند مخصّص — تعدّل من نافذة السند نفسه");
            }
            if (entry.Source != JournalEntrySource.Manual)
                return Result.Failure<int>($"هذا القيد مولَّد من ({entry.Source}) — تعدّل من نافذة المصدر");

            if (req.Lines == null || req.Lines.Count < 2)
                return Result.Failure<int>("القيد لازم سطرين على الأقل");

            var d = req.Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var c = req.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            if (Math.Round(d, 3) != Math.Round(c, 3))
                return Result.Failure<int>("القيد غير متوازن");

            var accountIds = req.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.Id) && a.IsActive).ToListAsync(ct);
            if (accounts.Count != accountIds.Count)
                return Result.Failure<int>("بعض الحسابات غير موجودة أو غير مفعّلة");
            var nonLeaf = accounts.FirstOrDefault(a => !a.IsLeaf);
            if (nonLeaf != null) return Result.Failure<int>($"الحساب '{nonLeaf.NameAr}' حساب رئيسي - لا يقبل قيوداً");

            // التحقق من تسعير العملة في نشرة الأسعار
            var currencyCheck = await EnsureCurrencyHasActiveBulletin(req.Currency, req.EntryDate, ct);
            if (currencyCheck != null) return Result.Failure<int>(currencyCheck);

            // ‎فحص قواعد الصناديق (سقوف + منع استخدامها في قيد عام)
            var cashBoxCheck = await CashBoxGuard.ValidateAsync(
                _db,
                req.Lines.Select(l => new CashBoxGuard.LineSnapshot(l.AccountId, l.IsDebit, l.Amount)).ToList(),
                req.Currency,
                req.VoucherTypeId,
                excludeJournalEntryId: entry.Id,
                ct);
            if (cashBoxCheck != null) return Result.Failure<int>(cashBoxCheck);

            // التحقق من نوع السند إن وُجد
            if (req.VoucherTypeId.HasValue)
            {
                var vt = await _db.JournalVoucherTypes.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == req.VoucherTypeId.Value, ct);
                if (vt == null) return Result.Failure<int>("نوع السند المختار غير موجود");
                if (!vt.IsEnabled) return Result.Failure<int>($"نوع السند '{vt.NameAr}' معطّل");
            }

            // إذا القيد مرحَّل، نُرجعه إلى مسودة قبل التعديل.
            // ثم نُعيد ترحيله فقط إذا طلب المستخدم — هذا يتيح فك الترحيل عند
            // إلغاء علامة "ترحيل فوري" أثناء التعديل.
            if (entry.Status == JournalEntryStatus.Posted) entry.Unpost();

            entry.UpdateBasic(req.EntryDate, req.Description, req.EntryType, req.Currency, req.VoucherTypeId);
            entry.ReplaceLines(req.Lines.Select(l =>
                (l.AccountId, l.IsDebit, l.Amount, l.Description)).ToList());

            if (req.PostImmediately)
                entry.Post(_currentUser.UserId?.ToString() ?? "system");

            await _db.SaveChangesAsync(ct);
            return Result.Success(entry.Id);
        }
        catch (UnbalancedJournalEntryException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    /// <summary>
    /// إذا كانت العملة غير العملة الرئيسية للنشرة المنشورة الأحدث، يجب أن يكون هناك سطر سعر صرف لها.
    /// إن لم توجد نشرة منشورة سارية أصلاً، يُرفض القيد بعملة غير IQD (الافتراضية للنظام).
    /// </summary>
    private async Task<string?> EnsureCurrencyHasActiveBulletin(string currency, DateTime entryDate, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        var atUtc = (entryDate.Kind == DateTimeKind.Utc ? entryDate : entryDate.ToUniversalTime())
            .Date.AddDays(1).AddTicks(-1);

        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= atUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (bulletin != null && string.Equals(bulletin.BaseCurrency, cur, StringComparison.OrdinalIgnoreCase))
            return null;

        if (bulletin == null)
        {
            if (cur == "IQD") return null;
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}. أصدِر نشرة أسعار وانشرها قبل حفظ القيد.";
        }

        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}'. أضف سعر صرف لها في النشرة أو أصدر نشرة جديدة تتضمنها قبل حفظ القيد.";

        return null;
    }
}
