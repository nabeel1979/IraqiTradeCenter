using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;

public record CreateFiscalYearCommand(string Name, DateTime StartDate, DateTime EndDate) : IRequest<int>;

public class CreateFiscalYearHandler : IRequestHandler<CreateFiscalYearCommand, int>
{
    private readonly IAccountingDbContext _db;
    public CreateFiscalYearHandler(IAccountingDbContext db) => _db = db;

    public async Task<int> Handle(CreateFiscalYearCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainException("اسم السنة المالية مطلوب");

        var overlap = await _db.FiscalYears.AsNoTracking().AnyAsync(f =>
            (req.StartDate >= f.StartDate && req.StartDate <= f.EndDate) ||
            (req.EndDate >= f.StartDate && req.EndDate <= f.EndDate) ||
            (req.StartDate <= f.StartDate && req.EndDate >= f.EndDate), ct);
        if (overlap)
            throw new DomainException("الفترة الزمنية تتداخل مع سنة مالية أخرى");

        var fy = FiscalYear.Create(req.Name, req.StartDate, req.EndDate);
        _db.FiscalYears.Add(fy);
        await _db.SaveChangesAsync(ct);
        return fy.Id;
    }
}
