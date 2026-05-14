using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;

public class GetTrialBalanceHandler : IRequestHandler<GetTrialBalanceQuery, List<TrialBalanceRowDto>>
{
    private readonly IAccountingDbContext _db;
    public GetTrialBalanceHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<TrialBalanceRowDto>> Handle(GetTrialBalanceQuery req, CancellationToken ct)
    {
        var lines = await (
            from line in _db.JournalEntryLines.AsNoTracking()
            join entry in _db.JournalEntries on line.JournalEntryId equals entry.Id
            join acc in _db.Accounts on line.AccountId equals acc.Id
            where entry.Status == JournalEntryStatus.Posted
               && entry.EntryDate >= req.FromDate && entry.EntryDate <= req.ToDate
               && acc.IsLeaf
            select new { line, acc }
        ).ToListAsync(ct);

        return lines
            .GroupBy(x => new { x.acc.Id, x.acc.Code, x.acc.NameAr, x.acc.Nature })
            .Select(g =>
            {
                var d = g.Where(x => x.line.IsDebit).Sum(x => x.line.Amount);
                var c = g.Where(x => !x.line.IsDebit).Sum(x => x.line.Amount);
                var bal = g.Key.Nature == AccountNature.Debit ? d - c : c - d;
                return new TrialBalanceRowDto
                {
                    AccountId = g.Key.Id, AccountCode = g.Key.Code, AccountName = g.Key.NameAr,
                    Debit = d, Credit = c, Balance = bal
                };
            })
            .OrderBy(r => r.AccountCode).ToList();
    }
}
