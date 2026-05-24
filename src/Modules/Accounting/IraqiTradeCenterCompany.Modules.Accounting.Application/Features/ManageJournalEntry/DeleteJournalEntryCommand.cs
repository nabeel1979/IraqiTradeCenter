using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;

public record DeleteJournalEntryCommand(int Id) : IRequest<Result<bool>>;

public class DeleteJournalEntryHandler : IRequestHandler<DeleteJournalEntryCommand, Result<bool>>
{
    private readonly IAccountingDbContext _db;
    public DeleteJournalEntryHandler(IAccountingDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(DeleteJournalEntryCommand req, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
        if (entry == null) return Result.Failure<bool>("القيد غير موجود");
        if (entry.Status == JournalEntryStatus.Reversed)
            return Result.Failure<bool>("لا يمكن حذف قيد معكوس");

        // ‎قفل قيود المناقلات بين الصناديق
        if (entry.ReferenceType == "CashBoxTransfer" || entry.ReferenceType == "CashBoxTransferReversal")
            return Result.Failure<bool>(
                "هذا القيد مولَّد من مناقلة بين صندوقَين — لا يُحذف من نافذة القيود اليومية. " +
                "افتح صفحة الصناديق ⇒ تبويب 'المناقلات' لإلغاء المناقلة أو التراجع عن الاستلام.");

        // ‎حارس السنة المالية النشطة: لا يُحذف قيد ينتمي لسنة مالية غير المُفَعَّلة.
        var activeFy = await _db.FiscalYears.AsNoTracking()
            .FirstOrDefaultAsync(f => f.IsActive, ct);
        if (activeFy != null)
        {
            var d = entry.EntryDate.Date;
            if (d < activeFy.StartDate.Date || d > activeFy.EndDate.Date)
            {
                return Result.Failure<bool>(
                    $"تاريخ هذا القيد ({d:yyyy-MM-dd}) خارج السنة المالية النشطة '{activeFy.Name}'. لحذفه، فعِّل السنة المالية المناسبة أولاً.");
            }
        }

        // ‎ممنوع الحذف من واجهة "القيود اليومية" إذا كان القيد مولّداً من سند
        // ‎مخصّص غير مختلط (Debit/Credit). أنواع السندات المختلطة (Mixed)
        // ‎تُحذف من نفس صفحة القيود اليومية.
        if (entry.VoucherTypeId.HasValue)
        {
            var vtNature = await _db.JournalVoucherTypes.AsNoTracking()
                .Where(v => v.Id == entry.VoucherTypeId.Value)
                .Select(v => (Domain.Enums.VoucherNature?)v.Nature)
                .FirstOrDefaultAsync(ct);
            if (vtNature != Domain.Enums.VoucherNature.Mixed)
                return Result.Failure<bool>("هذا القيد مولَّد من سند مخصّص — يُحذف من نافذة السند نفسه");
        }
        if (entry.Source != JournalEntrySource.Manual)
            return Result.Failure<bool>($"هذا القيد مولَّد من ({entry.Source}) — يُحذف من نافذة المصدر");

        entry.MarkAsDeleted();
        foreach (var line in entry.Lines) line.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
