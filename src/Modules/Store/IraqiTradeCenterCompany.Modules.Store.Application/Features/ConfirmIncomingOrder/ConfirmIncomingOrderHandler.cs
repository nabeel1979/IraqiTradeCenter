using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.ConfirmIncomingOrder;

public class ConfirmIncomingOrderHandler : IRequestHandler<ConfirmIncomingOrderCommand, Result<SalesInvoiceDto>>
{
    private readonly IStoreDbContext _store;
    private readonly IMediator _mediator;
    public ConfirmIncomingOrderHandler(IStoreDbContext store, IMediator mediator)
    {
        _store = store; _mediator = mediator;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(ConfirmIncomingOrderCommand req, CancellationToken ct)
    {
        var order = await _store.IncomingOrders.Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == req.OrderId, ct);
        if (order == null) return Result.Failure<SalesInvoiceDto>("الطلبية غير موجودة");

        order.AssignSalesRep(req.SalesRepId);
        await _store.SaveChangesAsync(ct);

        // ابني فاتورة من بنود الطلبية
        var lines = order.Items.Select(i => new CreateInvoiceLineRequest(
            i.ItemId, i.UnitOfMeasureId, i.Quantity, i.UnitPrice, 0)).ToList();

        return await _mediator.Send(new CreateSalesInvoiceCommand(
            order.CustomerId, req.SalesRepId, order.Id,
            req.TaxRate, req.DiscountPercentage, 0,
            $"من الطلبية {order.PlatformOrderNumber}", lines), ct);
    }
}
