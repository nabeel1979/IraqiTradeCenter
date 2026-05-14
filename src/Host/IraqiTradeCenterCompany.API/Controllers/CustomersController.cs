using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomerStatement;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class CustomersController : BaseApiController
{
    [HttpGet("{id}/statement")]
    public async Task<IActionResult> Statement(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => HandleResult(await Mediator.Send(new GetCustomerStatementQuery(id, from, to)));
}
