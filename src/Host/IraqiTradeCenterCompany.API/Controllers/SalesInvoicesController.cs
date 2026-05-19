using IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesInvoicesList;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.RecordPayment;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class SalesInvoicesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] int? customerId = null, [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var data = await Mediator.Send(new GetSalesInvoicesListQuery(
            pageNumber, pageSize, search, status, customerId, fromDate, toDate));
        return Ok(new { success = true, data });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpPost("{invoiceId}/payments")]
    public async Task<IActionResult> RecordPayment(int invoiceId, [FromBody] RecordPaymentBody body)
        => HandleResult(await Mediator.Send(new RecordPaymentCommand(
            invoiceId, body.Amount, body.PaymentMethod, body.ReferenceNumber, body.Notes)));

    public record RecordPaymentBody(decimal Amount, string PaymentMethod, string? ReferenceNumber, string? Notes);
}
