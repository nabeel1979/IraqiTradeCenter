using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.ConfirmIncomingOrder;

public record ConfirmIncomingOrderCommand(
    int OrderId, int SalesRepId, decimal TaxRate, decimal DiscountPercentage
) : IRequest<Result<SalesInvoiceDto>>;
