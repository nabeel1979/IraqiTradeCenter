using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.AddSalesRep;

public record AddSalesRepCommand(
    Guid UserId, string EmployeeCode, string FullName, string Phone,
    decimal BaseSalary, CommissionType CommissionType,
    decimal? FixedCommissionRate, string? Region,
    List<TierRequest>? Tiers
) : IRequest<Result<SalesRepDto>>;

public record TierRequest(decimal FromAmount, decimal ToAmount, decimal Rate);
