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

        entry.MarkAsDeleted();
        foreach (var line in entry.Lines) line.MarkAsDeleted();

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
