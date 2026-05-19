using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;

public record CreateCurrencyRateBulletinCommand(
    string Name,
    string BaseCurrency,
    DateTime EffectiveAt,
    string? Notes,
    bool PublishImmediately,
    List<CurrencyRateLinePayload> Lines
) : IRequest<Result<int>>;

public class CreateCurrencyRateBulletinHandler
    : IRequestHandler<CreateCurrencyRateBulletinCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateCurrencyRateBulletinHandler(IAccountingDbContext db, ICurrentUserService currentUser)
    {
        _db = db; _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(CreateCurrencyRateBulletinCommand req, CancellationToken ct)
    {
        try
        {
            var bulletin = CurrencyRateBulletin.Create(req.Name, req.BaseCurrency, req.EffectiveAt, req.Notes);

            foreach (var l in req.Lines ?? new())
            {
                if (!Enum.IsDefined(typeof(CurrencyRateOperation), l.Operation))
                    return Result.Failure<int>("نوع العملية غير صالح (1=ضرب، 2=قسمة)");
                bulletin.AddLine(l.Currency, l.Rate, (CurrencyRateOperation)l.Operation, l.Notes);
            }

            if (req.PublishImmediately)
                bulletin.Publish(_currentUser.UserId?.ToString() ?? "system");

            await _db.CurrencyRateBulletins.AddAsync(bulletin, ct);
            await _db.SaveChangesAsync(ct);
            return Result.Success(bulletin.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
