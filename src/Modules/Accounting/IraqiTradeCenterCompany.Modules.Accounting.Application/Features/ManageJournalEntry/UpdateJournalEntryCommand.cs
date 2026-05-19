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

            // التحقق من نوع السند إن وُجد
            if (req.VoucherTypeId.HasValue)
            {
                var vt = await _db.JournalVoucherTypes.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == req.VoucherTypeId.Value, ct);
                if (vt == null) return Result.Failure<int>("نوع السند المختار غير موجود");
                if (!vt.IsEnabled) return Result.Failure<int>($"نوع السند '{vt.NameAr}' معطّل");
            }

            // إذا القيد مرحَّل، نُرجعه إلى مسودة قبل التعديل
            var wasPosted = entry.Status == JournalEntryStatus.Posted;
            if (wasPosted) entry.Unpost();

            entry.UpdateBasic(req.EntryDate, req.Description, req.EntryType, req.Currency, req.VoucherTypeId);
            entry.ReplaceLines(req.Lines.Select(l =>
                (l.AccountId, l.IsDebit, l.Amount, l.Description)).ToList());

            // إعادة الترحيل إذا طُلب أو إذا كان مرحَّلاً أصلاً
            if (req.PostImmediately || wasPosted)
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
