using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;

public class PostJournalEntryHandler : IRequestHandler<PostJournalEntryCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public PostJournalEntryHandler(IAccountingDbContext db, IPeriodResolver periods, ICurrentUserService currentUser)
    {
        _db = db; _periods = periods; _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(PostJournalEntryCommand request, CancellationToken ct)
    {
        try
        {
            var (fyId, periodId) = await _periods.ResolveAsync(request.EntryDate, ct);

            var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.Id) && a.IsActive).ToListAsync(ct);
            if (accounts.Count != accountIds.Count)
                return Result.Failure<int>("بعض الحسابات غير موجودة أو غير مفعّلة");
            var nonLeaf = accounts.FirstOrDefault(a => !a.IsLeaf);
            if (nonLeaf != null) return Result.Failure<int>($"الحساب '{nonLeaf.NameAr}' حساب رئيسي - لا يقبل قيوداً");

            // التحقق من وجود نشرة أسعار منشورة سارية إذا كانت العملة أجنبية
            var currencyCheck = await EnsureCurrencyHasActiveBulletin(request.Currency, request.EntryDate, ct);
            if (currencyCheck != null) return Result.Failure<int>(currencyCheck);

            // ‎فحص قواعد الصناديق: منع استخدام حسابات الصناديق في قيد عام،
            // ‎واحترام سقوف المدين/الدائن لكل صندوق بكل عملة.
            var cashBoxCheck = await CashBoxGuard.ValidateAsync(
                _db,
                request.Lines.Select(l => new CashBoxGuard.LineSnapshot(l.AccountId, l.IsDebit, l.Amount)).ToList(),
                request.Currency,
                request.VoucherTypeId,
                excludeJournalEntryId: null,
                ct);
            if (cashBoxCheck != null) return Result.Failure<int>(cashBoxCheck);

            // معاملة صريحة لضمان: GetNextNumber + INSERT ذرّيان → يمنع تكرار رقم القيد
            // عند طلبات متزامنة. القفل sp_getapplock داخل GetNextJournalEntryNumberAsync
            // يبقى مرفوعاً حتى Commit/Rollback.
            await using var tx = await _db.BeginTransactionAsync(ct);

            // التحقق من نوع السند إن وُجد
            if (request.VoucherTypeId.HasValue)
            {
                var vt = await _db.JournalVoucherTypes.AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == request.VoucherTypeId.Value, ct);
                if (vt == null) return Result.Failure<int>("نوع السند المختار غير موجود");
                if (!vt.IsEnabled) return Result.Failure<int>($"نوع السند '{vt.NameAr}' معطّل");
            }

            var nextNum = await _db.GetNextJournalEntryNumberAsync(fyId, ct);
            var entryNumber = nextNum.ToString();

            // توليد تسلسل سند مستقل لكل نوع (PV-1, PV-2, RV-1 …) عند وجود VoucherTypeId
            int? voucherSeq = null;
            if (request.VoucherTypeId.HasValue)
            {
                voucherSeq = await _db.GetNextVoucherSequenceAsync(request.VoucherTypeId.Value, ct);
            }

            var entry = JournalEntry.Create(request.EntryDate, fyId, periodId,
                JournalEntrySource.Manual, request.Description,
                type: request.EntryType, currency: request.Currency,
                entryNumber: entryNumber, voucherTypeId: request.VoucherTypeId,
                voucherSequence: voucherSeq);

            foreach (var l in request.Lines)
            {
                if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Description);
                else entry.AddCredit(l.AccountId, l.Amount, l.Description);
            }

            // الترحيل التلقائي يحدث فقط إذا طلبه المستخدم وكان يملك صلاحية الترحيل.
            // المستخدم بدون صلاحية: يبقى القيد Draft ليُرحَّل لاحقاً من مستخدم آخر مخوَّل.
            // أكواد الصلاحية تطابق Auth/Permissions/PermissionRegistry.cs (لا يمكن الإشارة لها مباشرة لتجنّب التبعية بين المودولز).
            const string PostPermission = "Accounting.JournalEntries.Post";
            const string VoucherPostPermission = "Accounting.Vouchers.Post";
            var requiredPostPerm = request.VoucherTypeId.HasValue ? VoucherPostPermission : PostPermission;
            var canPost = _currentUser.IsSuperAdmin || _currentUser.HasPermission(requiredPostPerm);

            if (request.PostImmediately && canPost)
                entry.Post(_currentUser.UserId?.ToString() ?? "system");

            await _db.JournalEntries.AddAsync(entry, ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success(entry.Id);
        }
        catch (UnbalancedJournalEntryException ex) { return Result.Failure<int>(ex.Message); }
        catch (ClosedPeriodException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }

    /// <summary>
    /// إذا كانت العملة غير العملة الرئيسية للنشرة المنشورة الأحدث، يجب أن يكون هناك سطر سعر صرف لها.
    /// إن لم توجد نشرة منشورة سارية أصلاً، يُرفض القيد بعملة غير IQD (الافتراضية للنظام).
    /// </summary>
    private async Task<string?> EnsureCurrencyHasActiveBulletin(string currency, DateTime entryDate, CancellationToken ct)
    {
        var cur = (currency ?? "IQD").Trim().ToUpperInvariant();
        // نقطة سريانٍ: نهاية يوم القيد (لإتاحة استعمال نشرات اليوم)
        var atUtc = (entryDate.Kind == DateTimeKind.Utc ? entryDate : entryDate.ToUniversalTime())
            .Date.AddDays(1).AddTicks(-1);

        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= atUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        // إذا العملة هي العملة الرئيسية للنشرة، لا حاجة لسطر صرف
        if (bulletin != null && string.Equals(bulletin.BaseCurrency, cur, StringComparison.OrdinalIgnoreCase))
            return null;

        // إذا لم توجد نشرة منشورة، اقبل فقط العملة الافتراضية IQD
        if (bulletin == null)
        {
            if (cur == "IQD") return null;
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار — لا توجد نشرة منشورة سارية بتاريخ {entryDate:yyyy-MM-dd}. أصدِر نشرة أسعار وانشرها قبل حفظ القيد.";
        }

        // النشرة موجودة لكن العملة غير الرئيسية → يجب أن تحوي سطراً لها
        var hasLine = bulletin.Lines.Any(l => string.Equals(l.Currency, cur, StringComparison.OrdinalIgnoreCase));
        if (!hasLine)
            return $"العملة {cur} غير مُسعَّرة في نشرة الأسعار '{bulletin.Name}'. أضف سعر صرف لها في النشرة أو أصدر نشرة جديدة تتضمنها قبل حفظ القيد.";

        return null;
    }
}
