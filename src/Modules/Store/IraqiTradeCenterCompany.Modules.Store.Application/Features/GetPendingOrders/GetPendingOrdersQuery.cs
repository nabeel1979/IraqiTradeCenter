using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetPendingOrders;

public record GetPendingOrdersQuery(int PageNumber = 1, int PageSize = 20,
    OrderProcessingStatus? Status = null) : IRequest<PagedResult<IncomingOrderDto>>;
