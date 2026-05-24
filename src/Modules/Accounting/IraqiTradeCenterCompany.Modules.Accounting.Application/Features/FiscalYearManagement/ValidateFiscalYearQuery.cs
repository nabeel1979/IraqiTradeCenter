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

        // ‎الـ global query filter في DbContext يستثني المحذوفين (IsDeleted=1) تلقائياً.
        // ‎نُحضر تفاصيل القيود حتى يستطيع المستخدم فتحها ومعالجتها (ترحيلها أو حذفها)
        // ‎بدلاً من مجرد عدّ غامض كان يُلبس عليه ما إذا كان القيد محذوفاً فعلاً أم لا.
        var draftRefs = await (
            from e in _db.JournalEntries.AsNoTracking()
            where e.EntryDate >= fy.StartDate && e.EntryDate <= fy.EndDate
               && e.Status == JournalEntryStatus.Draft
            join vt in _db.JournalVoucherTypes.AsNoTracking()
                on e.VoucherTypeId equals vt.Id into vts
            from vt in vts.DefaultIfEmpty()
            orderby e.EntryDate, e.Id
            select new DraftJournalEntryRefDto
            {
                Id = e.Id,
                EntryNumber = e.EntryNumber,
                EntryDate = e.EntryDate,
                Description = e.Description,
                VoucherTypeCode = vt != null ? vt.Code : null,
                VoucherSequence = e.VoucherSequence,
            }
        ).ToListAsync(ct);

        result.DraftEntries = draftRefs.Count;
        result.DraftEntriesList = draftRefs;
        if (draftRefs.Count > 0)
            result.Issues.Add($"يوجد {draftRefs.Count} قيد غير مرحَّل (مسودة)");

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
