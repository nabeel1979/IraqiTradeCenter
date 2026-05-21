using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;

public record GetJournalEntryByIdQuery(int Id) : IRequest<JournalEntryDto?>;

public class GetJournalEntryByIdHandler : IRequestHandler<GetJournalEntryByIdQuery, JournalEntryDto?>
{
    private readonly IAccountingDbContext _db;
    public GetJournalEntryByIdHandler(IAccountingDbContext db) => _db = db;

    public async Task<JournalEntryDto?> Handle(GetJournalEntryByIdQuery req, CancellationToken ct)
    {
        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Include(e => e.VoucherType)
            .FirstOrDefaultAsync(e => e.Id == req.Id, ct);
        if (entry == null) return null;

        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accountNames = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.Code, a.NameAr }, ct);

        return new JournalEntryDto
        {
            Id = entry.Id,
            EntryNumber = entry.EntryNumber,
            EntryDate = entry.EntryDate,
            Status = entry.Status.ToString(),
            EntryType = entry.EntryType.ToString(),
            Currency = entry.Currency,
            Description = entry.Description,
            TotalDebit = entry.TotalDebit,
            TotalCredit = entry.TotalCredit,
            VoucherTypeId = entry.VoucherTypeId,
            VoucherTypeCode = entry.VoucherType?.Code,
            VoucherTypeName = entry.VoucherType?.NameAr,
            VoucherSequence = entry.VoucherSequence,
            VoucherNumber = (entry.VoucherSequence.HasValue && entry.VoucherType != null)
                ? $"{entry.VoucherType.Code}-{entry.VoucherSequence.Value}"
                : null,
            Source = entry.Source.ToString(),
            ReferenceType = entry.ReferenceType,
            ReferenceId = entry.ReferenceId,
            ReferenceNumber = entry.ReferenceNumber,
            Lines = entry.Lines.Select(l => new JournalLineDto
            {
                Id = l.Id,
                AccountId = l.AccountId,
                AccountName = accountNames.TryGetValue(l.AccountId, out var a)
                    ? $"{a.Code} - {a.NameAr}" : null,
                IsDebit = l.IsDebit,
                Amount = l.Amount,
                Description = l.Description
            }).ToList()
        };
    }
}
