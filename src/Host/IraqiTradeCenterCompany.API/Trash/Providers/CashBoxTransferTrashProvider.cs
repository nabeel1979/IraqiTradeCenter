using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

public class CashBoxTransferTrashProvider : ITrashProvider
{
    private readonly IAccountingDbContext _db;
    public CashBoxTransferTrashProvider(IAccountingDbContext db) { _db = db; }

    public string EntityType => "CashBoxTransfer";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await _db.CashBoxTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .Select(t => new
            {
                t.Id,
                t.TransferNumber,
                t.FromCashBoxId,
                t.ToCashBoxId,
                t.Currency,
                t.Amount,
                t.SendDate,
                t.DeletedAt,
                t.UpdatedBy,
            })
            .ToListAsync(ct);

        var boxIds = rows.SelectMany(r => new[] { r.FromCashBoxId, r.ToCashBoxId }).Distinct().ToList();
        var boxes = boxIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.CashBoxes.IgnoreQueryFilters().AsNoTracking()
                .Where(b => boxIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.NameAr, ct);

        return rows.Select(r => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "حوالة صندوق",
            Module = "المحاسبة",
            Icon = "ArrowRightLeft",
            EntityId = r.Id,
            Code = r.TransferNumber,
            DisplayName = $"{boxes.GetValueOrDefault(r.FromCashBoxId, "?")} ← {boxes.GetValueOrDefault(r.ToCashBoxId, "?")}",
            SubInfo = $"{r.Amount:N3} {r.Currency} · بتاريخ {r.SendDate:yyyy/MM/dd}",
            DeletedAt = r.DeletedAt,
            DeletedBy = r.UpdatedBy,
        }).ToList();
    }

    public async Task<Result> RestoreAsync(int id, CancellationToken ct)
    {
        var t = await _db.CashBoxTransfers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Failure("الحوالة غير موجودة");
        if (!t.IsDeleted) return Result.Failure("الحوالة ليست في السلة");

        // ‎عند استعادة الحوالة لا بد من استعادة كل قيودها الثلاثة الممكنة
        // ‎(إرسال + استلام إن وُجد + عكس الإلغاء إن وُجد)، وأيضاً أي قيود
        // ‎أخرى تشترك معها بـ ReferenceNumber (تاريخية مثل عكس استلام).
        var entries = await EntriesOwnedByTransferAsync(t, ct);

        t.Restore();
        foreach (var e in entries)
        {
            e.Restore();
            foreach (var l in e.Lines) l.Restore();
        }
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
    {
        var t = await _db.CashBoxTransfers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return Result.Failure("الحوالة غير موجودة");
        if (!t.IsDeleted) return Result.Failure("الحذف النهائي مسموح فقط من السلة");

        // ‎نحذف القيود المرتبطة أيضاً (مع أسطرها). لا بد من حذف الحوالة أوّلاً
        // ‎كي يسقط الـ FK (Send/Receive/Reversal) ثم نحذف القيود.
        var entries = await EntriesOwnedByTransferAsync(t, ct);
        var lines = entries.SelectMany(e => e.Lines).ToList();

        _db.CashBoxTransfers.Remove(t);
        _db.JournalEntryLines.RemoveRange(lines);
        _db.JournalEntries.RemoveRange(entries);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>
    /// جميع القيود التي تخصّ هذه المناقلة: المربوطة عبر FKs الثلاثة، أو عبر
    /// <c>ReferenceNumber == TransferNumber</c> مع نوع مرجع مناقلة. يُستخدم
    /// للاستعادة والحذف النهائي معاً لضمان عدم ترك قيود يتيمة.
    /// </summary>
    private async Task<List<JournalEntry>> EntriesOwnedByTransferAsync(
        CashBoxTransfer t, CancellationToken ct)
    {
        var idsFromFks = new HashSet<int> { t.SendJournalEntryId };
        if (t.ReceiveJournalEntryId.HasValue)  idsFromFks.Add(t.ReceiveJournalEntryId.Value);
        if (t.ReversalJournalEntryId.HasValue) idsFromFks.Add(t.ReversalJournalEntryId.Value);

        return await _db.JournalEntries.IgnoreQueryFilters()
            .Include(e => e.Lines)
            .Where(e => idsFromFks.Contains(e.Id)
                     || (e.ReferenceNumber == t.TransferNumber
                         && (e.ReferenceType == "CashBoxTransfer"
                             || e.ReferenceType == "CashBoxTransferReversal")))
            .ToListAsync(ct);
    }
}
