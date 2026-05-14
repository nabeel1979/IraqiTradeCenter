using AutoMapper;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;

public class GetAccountsTreeHandler : IRequestHandler<GetAccountsTreeQuery, List<AccountDto>>
{
    private readonly IAccountingDbContext _db;
    private readonly IMapper _mapper;
    public GetAccountsTreeHandler(IAccountingDbContext db, IMapper mapper) { _db = db; _mapper = mapper; }

    public async Task<List<AccountDto>> Handle(GetAccountsTreeQuery req, CancellationToken ct)
    {
        var all = await _db.Accounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Code).ToListAsync(ct);
        var dtos = _mapper.Map<List<AccountDto>>(all);
        var lookup = dtos.ToDictionary(a => a.Id);
        var roots = new List<AccountDto>();
        foreach (var d in dtos)
        {
            if (d.ParentId.HasValue && lookup.TryGetValue(d.ParentId.Value, out var parent))
                parent.Children.Add(d);
            else roots.Add(d);
        }
        return roots;
    }
}
