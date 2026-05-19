using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;

public record GetCurrencyRateBulletinByIdQuery(int Id) : IRequest<CurrencyRateBulletinDto?>;

public class GetCurrencyRateBulletinByIdHandler
    : IRequestHandler<GetCurrencyRateBulletinByIdQuery, CurrencyRateBulletinDto?>
{
    private readonly IAccountingDbContext _db;
    public GetCurrencyRateBulletinByIdHandler(IAccountingDbContext db) => _db = db;

    public async Task<CurrencyRateBulletinDto?> Handle(GetCurrencyRateBulletinByIdQuery req, CancellationToken ct)
    {
        var x = await _db.CurrencyRateBulletins.AsNoTracking()
            .Include(b => b.Lines)
            .FirstOrDefaultAsync(b => b.Id == req.Id, ct);
        if (x == null) return null;

        var nowUtc = DateTime.UtcNow;
        var defaultId = await _db.CurrencyRateBulletins.AsNoTracking()
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= nowUtc)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(ct);

        return GetCurrencyRateBulletinsHandler.MapToDto(x, defaultId);
    }
}

public record GetActiveCurrencyRateBulletinQuery(DateTime? At = null) : IRequest<CurrencyRateBulletinDto?>;

public class GetActiveCurrencyRateBulletinHandler
    : IRequestHandler<GetActiveCurrencyRateBulletinQuery, CurrencyRateBulletinDto?>
{
    private readonly IAccountingDbContext _db;
    public GetActiveCurrencyRateBulletinHandler(IAccountingDbContext db) => _db = db;

    public async Task<CurrencyRateBulletinDto?> Handle(GetActiveCurrencyRateBulletinQuery req, CancellationToken ct)
    {
        var at = (req.At ?? DateTime.UtcNow).ToUniversalTime();
        var x = await _db.CurrencyRateBulletins.AsNoTracking()
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= at)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);
        if (x == null) return null;
        return GetCurrencyRateBulletinsHandler.MapToDto(x, x.Id);
    }
}
