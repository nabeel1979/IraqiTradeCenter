using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesRepPerformance;

public record GetSalesRepPerformanceQuery(int SalesRepId, DateTime FromDate, DateTime ToDate)
    : IRequest<Result<SalesRepPerformanceDto>>;
