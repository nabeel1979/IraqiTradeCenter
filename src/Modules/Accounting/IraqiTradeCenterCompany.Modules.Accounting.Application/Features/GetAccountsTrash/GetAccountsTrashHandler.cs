using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTrash;

public class GetAccountsTrashHandler : IRequestHandler<GetAccountsTrashQuery, List<TrashedAccountDto>>
{
    private readonly IAccountingDbContext _db;
    public GetAccountsTrashHandler(IAccountingDbContext db) { _db = db; }

    public async Task<List<TrashedAccountDto>> Handle(GetAccountsTrashQuery req, CancellationToken ct)
    {
        // ‎نتجاوز query filter لاسترجاع المحذوفين، لكن نقصر النتيجة على IsDeleted=true فقط.
        var trashed = await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.IsDeleted)
            .OrderByDescending(a => a.DeletedAt)
            .ThenBy(a => a.Code)
            .ToListAsync(ct);

        // ‎نجلب الآباء المعنيّين في استعلام واحد لتفادي N+1.
        var parentIds = trashed.Where(a => a.ParentId.HasValue)
            .Select(a => a.ParentId!.Value).Distinct().ToList();
        var parents = parentIds.Count == 0
            ? new Dictionary<int, Account>()
            : await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
                .Where(a => parentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, ct);

        return trashed.Select(a =>
        {
            Account? parent = null;
            if (a.ParentId.HasValue) parents.TryGetValue(a.ParentId.Value, out parent);
            return new TrashedAccountDto
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                Type = a.Type,
                Nature = a.Nature,
                Level = a.Level,
                IsLeaf = a.IsLeaf,
                ParentId = a.ParentId,
                ParentCode = parent?.Code,
                ParentNameAr = parent?.NameAr,
                ParentIsDeleted = parent?.IsDeleted ?? false,
                DeletedAt = a.DeletedAt,
                DeletedBy = a.UpdatedBy,
            };
        }).ToList();
    }
}
