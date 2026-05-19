using System.Data;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record ValidateFiscalYearQuery(int FiscalYearId) : IRequest<FiscalYearValidationDto>;

public class ValidateFiscalYearHandler : IRequestHandler<ValidateFiscalYearQuery, FiscalYearValidationDto>
{
    private readonly IAccountingDbContext _db;
    public ValidateFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<FiscalYearValidationDto> Handle(ValidateFiscalYearQuery req, CancellationToken ct)
    {
        var fy = await _db.FiscalYears.AsNoTracking().FirstOrDefaultAsync(f => f.Id == req.FiscalYearId, ct);
        var result = new FiscalYearValidationDto { CanClose = false };

        if (fy == null)
        {
            result.Issues.Add("السنة المالية غير موجودة");
            return result;
        }

        if (fy.IsClosed)
        {
            result.Issues.Add("السنة المالية مغلقة بالفعل");
            return result;
        }

        var draft = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.EntryDate >= fy.StartDate && e.EntryDate <= fy.EndDate
                     && e.Status == JournalEntryStatus.Draft)
            .CountAsync(ct);
        result.DraftEntries = draft;
        if (draft > 0) result.Issues.Add($"يوجد {draft} قيد غير مرحَّل (مسودة)");

        var lines = await (
            from line in _db.JournalEntryLines.AsNoTracking()
            join entry in _db.JournalEntries on line.JournalEntryId equals entry.Id
            where entry.Status == JournalEntryStatus.Posted
                && entry.EntryDate >= fy.StartDate && entry.EntryDate <= fy.EndDate
            select new { line.IsDebit, line.Amount }
        ).ToListAsync(ct);

        var totalD = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        var totalC = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
        result.Difference = totalD - totalC;
        result.IsBalanced = Math.Abs(result.Difference) < 0.01m;
        if (!result.IsBalanced)
            result.Issues.Add($"القيود المرحَّلة غير متوازنة (فرق: {result.Difference:N2})");

        result.CanClose = result.Issues.Count == 0;
        return result;
    }
}
