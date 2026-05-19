using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;

public record UpdateCurrencyRateBulletinCommand(
    int Id,
    string Name,
    string BaseCurrency,
    DateTime EffectiveAt,
    string? Notes,
    List<CurrencyRateLinePayload> Lines
) : IRequest<Result>;

public class UpdateCurrencyRateBulletinHandler
    : IRequestHandler<UpdateCurrencyRateBulletinCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public UpdateCurrencyRateBulletinHandler(IAccountingDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateCurrencyRateBulletinCommand req, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (b == null) return Result.Failure("النشرة غير موجودة");

        try
        {
            b.UpdateMeta(req.Name, req.BaseCurrency, req.EffectiveAt, req.Notes);
            // استبدال الأسطر بالكامل
            foreach (var oldLine in b.Lines.ToList())
                _db.CurrencyRateLines.Remove(oldLine);
            b.ClearLines();

            foreach (var l in req.Lines ?? new())
            {
                if (!Enum.IsDefined(typeof(CurrencyRateOperation), l.Operation))
                    return Result.Failure("نوع العملية غير صالح (1=ضرب، 2=قسمة)");
                b.AddLine(l.Currency, l.Rate, (CurrencyRateOperation)l.Operation, l.Notes);
            }

            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Message); }
    }
}
