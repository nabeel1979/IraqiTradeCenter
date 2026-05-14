using AutoMapper;
using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.AddSalesRep;

public class AddSalesRepHandler : IRequestHandler<AddSalesRepCommand, Result<SalesRepDto>>
{
    private readonly IStoreDbContext _store;
    private readonly IMapper _mapper;
    public AddSalesRepHandler(IStoreDbContext store, IMapper mapper) { _store = store; _mapper = mapper; }

    public async Task<Result<SalesRepDto>> Handle(AddSalesRepCommand req, CancellationToken ct)
    {
        try
        {
            if (await _store.SalesReps.AnyAsync(s => s.EmployeeCode == req.EmployeeCode, ct))
                return Result.Failure<SalesRepDto>("الرقم الوظيفي موجود مسبقاً");

            var rep = SalesRep.Create(req.UserId, req.EmployeeCode, req.FullName, req.Phone,
                req.BaseSalary, req.CommissionType, req.FixedCommissionRate);

            await _store.SalesReps.AddAsync(rep, ct);
            await _store.SaveChangesAsync(ct);

            if (req.Tiers != null)
                foreach (var t in req.Tiers)
                    rep.AddTier(t.FromAmount, t.ToAmount, t.Rate);
            await _store.SaveChangesAsync(ct);

            return Result.Success(_mapper.Map<SalesRepDto>(rep));
        }
        catch (DomainException ex) { return Result.Failure<SalesRepDto>(ex.Message); }
    }
}
