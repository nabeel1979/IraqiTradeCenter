using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

public class CashBoxTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public CashBoxTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "CashBox";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.CashBoxes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.IsDeleted)
            .OrderByDescending(x => x.DeletedAt)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.NameAr,
                x.AccountId,
                x.DeletedAt,
                x.UpdatedBy,
            })
            .ToListAsync(ct);

        // ‎جلب أسماء الحسابات المرتبطة بشكل دفعة واحدة (تفادي N+1).
        var accountIds = rows.Select(r => r.AccountId).Distinct().ToList();
        var accountNames = await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => $"{a.Code} · {a.NameAr}", ct);

        return rows.Select(r => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "صندوق",
            Module = "المحاسبة",
            Icon = "Wallet",
            EntityId = r.Id,
            Code = r.Code,
            DisplayName = r.NameAr,
            SubInfo = accountNames.TryGetValue(r.AccountId, out var n) ? $"حساب: {n}" : null,
            DeletedAt = r.DeletedAt,
            DeletedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var entity = await _db.CashBoxes.IgnoreQueryFilters()
            .Include(x => x.Currencies)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure("الصندوق غير موجود");
        if (!entity.IsDeleted) return Result.Failure("الصندوق ليس في السلة");

        // ‎الحساب المرتبط بالصندوق إذا كان محذوفاً لا تتم الاستعادة (حلقة مفقودة).
        var linkedAccount = await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == entity.AccountId, ct);
        if (linkedAccount != null && linkedAccount.IsDeleted)
            return Result.Failure($"الحساب المرتبط ({linkedAccount.Code}) في السلة — استعده أولاً");

        entity.Restore();
        foreach (var c in entity.Currencies) c.Restore();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _db.CashBoxes.IgnoreQueryFilters()
            .Include(x => x.Currencies)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure("الصندوق غير موجود");
        if (!entity.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");

        // ‎فحوصات مرجعية على الجداول التي تشير للصندوق.
        var hasTransfers = await _db.CashBoxTransfers.IgnoreQueryFilters()
            .AnyAsync(t => t.FromCashBoxId == id || t.ToCashBoxId == id, ct);
        if (hasTransfers)
            return Result.Failure("لا يمكن الحذف النهائي — للصندوق حوالات مسجَّلة.");

        _db.CashBoxCurrencies.RemoveRange(entity.Currencies);
        _db.CashBoxes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
