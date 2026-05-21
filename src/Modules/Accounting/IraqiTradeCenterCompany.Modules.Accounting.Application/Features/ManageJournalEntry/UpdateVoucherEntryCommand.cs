using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;

/// <summary>
/// تحديث قيد مولَّد من سند مخصّص (سند قبض/سند دفع/…).
///   - يطلب أن يكون القيد فعلاً لديه VoucherTypeId.
///   - يستقبل بنوداً جديدة (سطرين فقط في معظم الحالات: الصندوق + الحساب المقابل).
///   - يكرر نفس قواعد التحقّق المستخدمة في UpdateJournalEntry (العملة، الحسابات، التوازن).
///
/// نقطة النهاية: PUT /api/accounting/vouchers/{id}
/// </summary>
public record UpdateVoucherEntryCommand(
    int Id,
    DateTime EntryDate,
    string Description,
    string Currency,
    List<UpdateJournalLine> Lines,
    bool PostImmediately = true
) : IRequest<Result<int>>;

public class UpdateVoucherEntryHandler : IRequestHandler<UpdateVoucherEntryCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService _currentUser;

    public UpdateVoucherEntryHandler(IAccountingDbContext db,
        IraqiTradeCenterCompany.SharedKernel.Interfaces.ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(UpdateVoucherEntryCommand req, CancellationToken ct)
    {
        try
        {
            var entry = await _db.JournalEntries.Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
            if (entry == null) return Result.Failure<int>("القيد غير موجود");
            if (entry.Status == JournalEntryStatus.Reversed)
                return Result.Failure<int>("لا يمكن تعديل قيد معكوس");

            if (!entry.VoucherTypeId.HasValue)
                return Result.Failure<int>("هذا القيد ليس مولَّداً من سند — استخدم تعديل القيد العادي");

            if (req.Lines == null || req.Lines.Count < 2)
                return Result.Failure<int>("السند لازم سطرين على الأقل");

            var d = req.Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var c = req.Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            if (Math.Round(d, 3) != Math.Round(c, 3))
                return Result.Failure<int>("السند غير متوازن");

            var accountIds = req.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.Id) && a.IsActive).ToListAsync(ct);
            if (accounts.Count != accountIds.Count)
                return Result.Failure<int>("بعض الحسابات غير موجودة أو غير مفعّلة");
            var nonLeaf = accounts.FirstOrDefault(a => !a.IsLeaf);
            if (nonLeaf != null) return Result.Failure<int>($"الحساب '{nonLeaf.NameAr}' حساب رئيسي - لا يقبل قيوداً");

            // التحقق من تسعير العملة
            var currencyCheck = await EnsureCurrencyHasActiveBulletin(req.Currency, req.EntryDate, ct);
            if (currencyCheck != null) return Result.Failure<int>(currencyCheck);

            // ‎فحص قواعد الصناديق (سقوف + منع استخدامها في قيد غير سند)
            var cashBoxCheck = await CashBoxGuard.ValidateAsync(
                _db,
                req.Lines.Select(l => new CashBoxGuard.LineSnapshot(l.AccountId, l.IsDebit, l.Amount)).ToList(),
                req.Currency,
                entry.VoucherTypeId,
                excludeJournalEntryId: entry.Id,
                ct);
            if (cashBoxCheck != null) return Result.Failure<int>(cashBoxCheck);

            var wasPosted = entry.Status == JournalEntryStatus.Posted;
            if (wasPosted) entry.Unpost();

            entry.UpdateBasic(req.EntryDate, req.Description, entry.EntryType, req.Currency, entry.VoucherTypeId);
            entry.ReplaceLines(req.Lines.Select(l =>
                (l.AccountId, l.IsDebit, l.Amount, l.Description)).ToList());

            if (req.PostImmediately || wasPosted)
                entry.Post(_currentUser.UserId?.ToString() ?? "system");

            await _db.SaveChangesAsync(ct);
            return Result.Success(entry.Id);
        }
        catch (UnbalancedJournalEntryException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

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
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}.";
        }

        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}'.";

        return null;
    }
}

/// <summary>أمر حذف قيد سند مخصّص (يتجاوز قيد منع حذف القيود المُدارة في DeleteJournalEntryCommand).</summary>
public record DeleteVoucherEntryCommand(int Id) : IRequest<Result<bool>>;

public class DeleteVoucherEntryHandler : IRequestHandler<DeleteVoucherEntryCommand, Result<bool>>
{
    private readonly IAccountingDbContext _db;
    public DeleteVoucherEntryHandler(IAccountingDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteVoucherEntryCommand req, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
        if (entry == null) return Result.Failure<bool>("القيد غير موجود");
        if (entry.Status == JournalEntryStatus.Reversed)
            return Result.Failure<bool>("لا يمكن حذف قيد معكوس");
        if (!entry.VoucherTypeId.HasValue)
            return Result.Failure<bool>("هذا القيد ليس سنداً — استخدم حذف القيد العادي");

        entry.MarkAsDeleted();
        foreach (var line in entry.Lines) line.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
