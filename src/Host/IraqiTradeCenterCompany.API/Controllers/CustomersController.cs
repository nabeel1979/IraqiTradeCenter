using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomerStatement;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomersList;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class CustomersController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] bool? activeOnly = true)
    {
        var data = await Mediator.Send(new GetCustomersListQuery(pageNumber, pageSize, search, activeOnly));
        return Ok(new { success = true, data });
    }

    [HttpGet("{id}/statement")]
    public async Task<IActionResult> Statement(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => HandleResult(await Mediator.Send(new GetCustomerStatementQuery(id, from, to)));
}
