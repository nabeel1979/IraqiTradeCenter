using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

public class FiscalYearTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public FiscalYearTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "FiscalYear";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.FiscalYears.IgnoreQueryFilters().AsNoTracking()
            .Where(f => f.IsDeleted)
            .OrderByDescending(f => f.DeletedAt)
            .Select(f => new { f.Id, f.Name, f.StartDate, f.EndDate, f.DeletedAt, f.UpdatedBy })
            .ToListAsync(ct);

        return rows.Select(r => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "سنة مالية",
            Module = "المحاسبة",
            Icon = "CalendarRange",
            EntityId = r.Id,
            DisplayName = r.Name,
            SubInfo = $"{r.StartDate:yyyy/MM/dd} → {r.EndDate:yyyy/MM/dd}",
            DeletedAt = r.DeletedAt,
            DeletedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var fy = await _db.FiscalYears.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fy is null) return Result.Failure("السنة المالية غير موجودة");
        if (!fy.IsDeleted) return Result.Failure("السنة ليست في السلة");
        fy.Restore();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var fy = await _db.FiscalYears.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (fy is null) return Result.Failure("السنة المالية غير موجودة");
        if (!fy.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");

        var refByEntry = await _db.JournalEntries.IgnoreQueryFilters()
            .AnyAsync(e => e.FiscalYearId == id, ct);
        if (refByEntry)
            return Result.Failure("لا يمكن الحذف النهائي — هناك قيود مرتبطة (نشطة أو محذوفة).");

        var periods = await _db.AccountingPeriods.IgnoreQueryFilters()
            .Where(p => p.FiscalYearId == id).ToListAsync(ct);
        _db.AccountingPeriods.RemoveRange(periods);
        _db.FiscalYears.Remove(fy);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
