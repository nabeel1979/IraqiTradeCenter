using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomerStatement;

public record GetCustomerStatementQuery(int CustomerId, DateTime FromDate, DateTime ToDate)
    : IRequest<Result<CustomerStatementDto>>;
