using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

/// <summary>
/// سلة القيود — تشمل القيود اليومية والسندات (يتم التمييز عبر <c>VoucherTypeId</c>).
/// عند الاستعادة، نُعيد كذلك أسطر القيد المرتبطة لاتساق المجاميع.
/// </summary>
public class JournalEntryTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public JournalEntryTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "JournalEntry";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        // ‎القيود المرتبطة بمناقلات صناديق (إرسال/استلام/عكس) لا تظهر هنا — مالكها
        // ‎هو المناقلة، وتُستعاد/تُحذف نهائياً من سلة المناقلات. عرضها هنا يُسبِّب
        // ‎تعارضاً (FK constraint) عند الحذف النهائي، وحالات معلَّقة عند الاستعادة.
        var transferLinkedIds = await _db.CashBoxTransfers.IgnoreQueryFilters().AsNoTracking()
            .SelectMany(t => new[] { (int?)t.SendJournalEntryId, t.ReceiveJournalEntryId, t.ReversalJournalEntryId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToListAsync(ct);

        var rows = await _db.JournalEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.IsDeleted && !transferLinkedIds.Contains(e.Id))
            .OrderByDescending(e => e.DeletedAt)
            .Select(e => new
            {
                e.Id,
                e.EntryNumber,
                e.EntryDate,
                e.Description,
                e.VoucherTypeId,
                e.TotalDebit,
                e.Currency,
                e.DeletedAt,
                e.UpdatedBy,
            })
            .ToListAsync(ct);

        // ‎جلب أسماء أنواع السندات في استعلام واحد.
        var voucherTypeIds = rows.Where(r => r.VoucherTypeId.HasValue)
            .Select(r => r.VoucherTypeId!.Value).Distinct().ToList();
        var voucherTypes = voucherTypeIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.JournalVoucherTypes.IgnoreQueryFilters().AsNoTracking()
                .Where(v => voucherTypeIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.NameAr, ct);

        return rows.Select(r =>
        {
            var isVoucher = r.VoucherTypeId.HasValue;
            string label;
            if (isVoucher && voucherTypes.TryGetValue(r.VoucherTypeId!.Value, out var vt))
                label = vt;
            else
                label = isVoucher ? "سند" : "قيد يومي";

            return new TrashItemDto
            {
                EntityType = EntityType,
                EntityTypeLabel = label,
                Module = "المحاسبة",
                Icon = isVoucher ? "FileText" : "BookOpen",
                EntityId = r.Id,
                Code = r.EntryNumber,
                DisplayName = string.IsNullOrWhiteSpace(r.Description) ? "قيد بدون وصف" : r.Description,
                SubInfo = $"بتاريخ {r.EntryDate:yyyy/MM/dd} · {r.TotalDebit:N3} {r.Currency}",
                DeletedAt = r.DeletedAt,
                DeletedBy = r.UpdatedBy,
            };
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.IgnoreQueryFilters()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return Result.Failure("القيد غير موجود");
        if (!entry.IsDeleted) return Result.Failure("القيد ليس في السلة");

        // ‎لا تُستعاد قيود مناقلات الصناديق من هنا — مالكها هو سجل المناقلة.
        if (await IsLinkedToCashBoxTransferAsync(id, ct))
            return Result.Failure(
                "هذا القيد مولَّد من مناقلة صندوق — يُستعاد من سلة المناقلات (سيُستعاد القيد تلقائياً مع المناقلة).");

        // ‎التأكد من أن الفترة المحاسبية ما زالت مفتوحة قبل إعادة قيد محذوف إليها.
        var period = await _db.AccountingPeriods.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == entry.AccountingPeriodId, ct);
        if (period != null && period.Status != PeriodStatus.Open)
            return Result.Failure("لا يمكن استعادة القيد — الفترة المحاسبية ليست مفتوحة.");

        entry.Restore();
        foreach (var line in entry.Lines) line.Restore();
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.IgnoreQueryFilters()
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return Result.Failure("القيد غير موجود");
        if (!entry.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");

        // ‎التحقّق من ربط القيد بأي مناقلة صندوق (إرسال/استلام/عكس). الحذف يكون
        // ‎عبر الحذف النهائي للمناقلة نفسها (CashBoxTransferTrashProvider) كي
        // ‎تُمسح القيود الثلاثة معاً ولا تُترك سجلات يتيمة.
        if (await IsLinkedToCashBoxTransferAsync(id, ct))
            return Result.Failure(
                "لا يمكن الحذف النهائي لهذا القيد — هو مرتبط بمناقلة صندوق. " +
                "احذف المناقلة نهائياً من سلة المناقلات وستُحذف قيودها معاً.");

        _db.JournalEntryLines.RemoveRange(entry.Lines);
        _db.JournalEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Task<bool> IsLinkedToCashBoxTransferAsync(int journalEntryId, CancellationToken ct) =>
        _db.CashBoxTransfers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(t =>
                t.SendJournalEntryId    == journalEntryId ||
                t.ReceiveJournalEntryId == journalEntryId ||
                t.ReversalJournalEntryId == journalEntryId, ct);
}
