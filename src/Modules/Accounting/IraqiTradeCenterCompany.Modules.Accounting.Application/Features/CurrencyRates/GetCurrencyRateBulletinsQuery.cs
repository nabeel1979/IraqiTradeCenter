using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;

public record GetCurrencyRateBulletinsQuery(int? Status = null, bool IncludeArchived = false)
    : IRequest<List<CurrencyRateBulletinDto>>;

public class GetCurrencyRateBulletinsHandler
    : IRequestHandler<GetCurrencyRateBulletinsQuery, List<CurrencyRateBulletinDto>>
{
    private readonly IAccountingDbContext _db;
    public GetCurrencyRateBulletinsHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<CurrencyRateBulletinDto>> Handle(GetCurrencyRateBulletinsQuery req, CancellationToken ct)
    {
        IQueryable<Domain.Entities.CurrencyRateBulletin> q = _db.CurrencyRateBulletins.AsNoTracking()
            .Include(x => x.Lines);

        if (req.Status.HasValue)
            q = q.Where(x => (int)x.Status == req.Status.Value);
        else if (!req.IncludeArchived)
            q = q.Where(x => x.Status != CurrencyRateBulletinStatus.Archived);

        var list = await q.OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.Id).ToListAsync(ct);

        // النشرة الافتراضية = أحدث منشورة لها EffectiveAt <= now
        var nowUtc = DateTime.UtcNow;
        var defaultId = list
            .Where(x => x.Status == CurrencyRateBulletinStatus.Published && x.EffectiveAt <= nowUtc)
            .OrderByDescending(x => x.EffectiveAt).ThenByDescending(x => x.Id)
            .FirstOrDefault()?.Id;

        return list.Select(x => MapToDto(x, defaultId)).ToList();
    }

    internal static CurrencyRateBulletinDto MapToDto(Domain.Entities.CurrencyRateBulletin x, int? defaultId)
        => new()
        {
            Id = x.Id,
            Name = x.Name,
            BaseCurrency = x.BaseCurrency,
            EffectiveAt = x.EffectiveAt,
            Status = (int)x.Status,
            StatusText = x.Status.ToString(),
            PublishedAt = x.PublishedAt,
            PublishedBy = x.PublishedBy,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt,
            CreatedBy = x.CreatedBy,
            UpdatedAt = x.UpdatedAt,
            IsDefault = defaultId.HasValue && defaultId.Value == x.Id,
            Lines = x.Lines.OrderBy(l => l.Currency).Select(l => new CurrencyRateLineDto
            {
                Id = l.Id,
                Currency = l.Currency,
                Rate = l.Rate,
                Operation = (int)l.Operation,
                OperationText = l.Operation.ToString(),
                Notes = l.Notes
            }).ToList()
        };
}
