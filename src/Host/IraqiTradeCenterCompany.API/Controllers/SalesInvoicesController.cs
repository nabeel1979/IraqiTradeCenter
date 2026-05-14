using IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.RecordPayment;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class SalesInvoicesController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpPost("{invoiceId}/payments")]
    public async Task<IActionResult> RecordPayment(int invoiceId, [FromBody] RecordPaymentBody body)
        => HandleResult(await Mediator.Send(new RecordPaymentCommand(
            invoiceId, body.Amount, body.PaymentMethod, body.ReferenceNumber, body.Notes)));

    public record RecordPaymentBody(decimal Amount, string PaymentMethod, string? ReferenceNumber, string? Notes);
}
