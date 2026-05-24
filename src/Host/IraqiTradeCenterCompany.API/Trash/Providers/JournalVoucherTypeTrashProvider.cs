using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

public class JournalVoucherTypeTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public JournalVoucherTypeTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "JournalVoucherType";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.JournalVoucherTypes.IgnoreQueryFilters().AsNoTracking()
            .Where(v => v.IsDeleted)
            .OrderByDescending(v => v.DeletedAt)
            .Select(v => new { v.Id, v.Code, v.NameAr, v.IsSystem, v.DeletedAt, v.UpdatedBy })
            .ToListAsync(ct);

        return rows.Select(r => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "نوع سند",
            Module = "المحاسبة",
            Icon = "Tag",
            EntityId = r.Id,
            Code = r.Code,
            DisplayName = r.NameAr,
            SubInfo = r.IsSystem ? "نوع نظامي" : null,
            DeletedAt = r.DeletedAt,
            DeletedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var entity = await _db.JournalVoucherTypes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (entity is null) return Result.Failure("نوع السند غير موجود");
        if (!entity.IsDeleted) return Result.Failure("نوع السند ليس في السلة");
        entity.Restore();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _db.JournalVoucherTypes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        if (entity is null) return Result.Failure("نوع السند غير موجود");
        if (!entity.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");
        if (entity.IsSystem)
            return Result.Failure("لا يمكن الحذف النهائي لنوع سند نظامي.");

        var refByEntry = await _db.JournalEntries.IgnoreQueryFilters()
            .AnyAsync(e => e.VoucherTypeId == id, ct);
        if (refByEntry)
            return Result.Failure("لا يمكن الحذف النهائي — نوع السند مستخدم في قيود.");

        _db.JournalVoucherTypes.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
