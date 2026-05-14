using IraqiTradeCenterCompany.Modules.Store.Application.Features.AddSalesRep;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.CalculateCommission;
using IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesRepPerformance;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class SalesRepsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddSalesRepCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpPost("{id}/calculate-commission")]
    public async Task<IActionResult> CalculateCommission(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => HandleResult(await Mediator.Send(new CalculateCommissionCommand(id, from, to)));

    [HttpGet("{id}/performance")]
    public async Task<IActionResult> Performance(int id, [FromQuery] DateTime from, [FromQuery] DateTime to)
        => HandleResult(await Mediator.Send(new GetSalesRepPerformanceQuery(id, from, to)));
}
