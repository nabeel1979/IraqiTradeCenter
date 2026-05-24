using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

public class CurrencyRateBulletinTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public CurrencyRateBulletinTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "CurrencyRateBulletin";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.CurrencyRateBulletins.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.IsDeleted)
            .OrderByDescending(b => b.DeletedAt)
            .Select(b => new { b.Id, b.Name, b.BaseCurrency, b.EffectiveAt, b.DeletedAt, b.UpdatedBy })
            .ToListAsync(ct);

        return rows.Select(r => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "نشرة أسعار عملات",
            Module = "المحاسبة",
            Icon = "Coins",
            EntityId = r.Id,
            DisplayName = r.Name,
            SubInfo = $"العملة الأساس: {r.BaseCurrency} · سرى من {r.EffectiveAt:yyyy/MM/dd}",
            DeletedAt = r.DeletedAt,
            DeletedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return Result.Failure("النشرة غير موجودة");
        if (!b.IsDeleted) return Result.Failure("النشرة ليست في السلة");
        b.Restore();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (b is null) return Result.Failure("النشرة غير موجودة");
        if (!b.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");

        var lines = await _db.CurrencyRateLines.IgnoreQueryFilters()
            .Where(l => l.CurrencyRateBulletinId == id).ToListAsync(ct);
        _db.CurrencyRateLines.RemoveRange(lines);
        _db.CurrencyRateBulletins.Remove(b);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
