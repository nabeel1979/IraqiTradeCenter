using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;

public record CreateSalesInvoiceCommand(
    int CustomerId, int? SalesRepId, int? IncomingOrderId,
    decimal TaxRate, decimal DiscountPercentage, decimal DiscountAmount,
    string? Notes,
    List<CreateInvoiceLineRequest> Lines
) : IRequest<Result<SalesInvoiceDto>>;

public record CreateInvoiceLineRequest(
    int ItemId, int UnitOfMeasureId, decimal Quantity,
    decimal? UnitPriceOverride,    // اختياري - يستخدم سعر المادة إذا null
    decimal LineDiscount
);
