using IraqiTradeCenterCompany.Modules.Store.Application.Features.ConfirmIncomingOrder;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetPendingOrders;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class IncomingOrdersController : BaseApiController
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] OrderProcessingStatus? status = null)
    {
        var data = await Mediator.Send(new GetPendingOrdersQuery(pageNumber, pageSize, status));
        return Ok(new { success = true, data });
    }

    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> Confirm(int id, [FromBody] ConfirmBody body)
        => HandleResult(await Mediator.Send(new ConfirmIncomingOrderCommand(
            id, body.SalesRepId, body.TaxRate, body.DiscountPercentage)));

    public record ConfirmBody(int SalesRepId, decimal TaxRate, decimal DiscountPercentage);
}
