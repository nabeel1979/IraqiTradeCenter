using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetJournalEntriesList;

public class GetJournalEntriesListHandler : IRequestHandler<GetJournalEntriesListQuery, PagedResult<JournalEntryDto>>
{
    private readonly IAccountingDbContext _db;
    public GetJournalEntriesListHandler(IAccountingDbContext db) => _db = db;

    public async Task<PagedResult<JournalEntryDto>> Handle(GetJournalEntriesListQuery req, CancellationToken ct)
    {
        var q = _db.JournalEntries.AsNoTracking()
            .Include(e => e.Lines)
            .Include(e => e.VoucherType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
        {
            var t = req.SearchTerm.Trim();
            q = q.Where(e => e.EntryNumber.Contains(t) || e.Description.Contains(t));
        }
        if (!string.IsNullOrWhiteSpace(req.Status) &&
            Enum.TryParse<JournalEntryStatus>(req.Status, true, out var s))
            q = q.Where(e => e.Status == s);
        if (req.FromDate.HasValue) q = q.Where(e => e.EntryDate >= req.FromDate);
        if (req.ToDate.HasValue)   q = q.Where(e => e.EntryDate <= req.ToDate);
        if (req.VoucherTypeId.HasValue) q = q.Where(e => e.VoucherTypeId == req.VoucherTypeId.Value);

        var total = await q.CountAsync(ct);
        var entries = await q.OrderByDescending(e => e.EntryDate).ThenByDescending(e => e.Id)
            .Skip((req.PageNumber - 1) * req.PageSize).Take(req.PageSize)
            .ToListAsync(ct);

        // جلب أسماء الحسابات لجميع الأسطر
        var accountIds = entries.SelectMany(e => e.Lines).Select(l => l.AccountId).Distinct().ToList();
        var accountNames = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.NameAr, ct);

        var dtos = entries.Select(e => new JournalEntryDto
        {
            Id = e.Id,
            EntryNumber = e.EntryNumber,
            EntryDate = e.EntryDate,
            Status = e.Status.ToString(),
            EntryType = e.EntryType.ToString(),
            Currency = e.Currency,
            Description = e.Description,
            TotalDebit = e.TotalDebit,
            TotalCredit = e.TotalCredit,
            VoucherTypeId = e.VoucherTypeId,
            VoucherTypeCode = e.VoucherType?.Code,
            VoucherTypeName = e.VoucherType?.NameAr,
            Source = e.Source.ToString(),
            ReferenceType = e.ReferenceType,
            ReferenceId = e.ReferenceId,
            ReferenceNumber = e.ReferenceNumber,
            Lines = e.Lines.Select(l => new JournalLineDto
            {
                Id = l.Id,
                AccountId = l.AccountId,
                AccountName = accountNames.GetValueOrDefault(l.AccountId),
                IsDebit = l.IsDebit,
                Amount = l.Amount,
                Description = l.Description
            }).ToList()
        }).ToList();

        return new PagedResult<JournalEntryDto>
        {
            Items = dtos, TotalCount = total,
            PageNumber = req.PageNumber, PageSize = req.PageSize
        };
    }
}
