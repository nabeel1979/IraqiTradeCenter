using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CashBoxes;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// إدارة الصناديق (الخزائن) النقدية: لكل صندوق حساب من الدليل المحاسبي،
/// قائمة عملات مدعومة، وحدود (سقف) دائنة/مدينة لكل عملة.
/// </summary>
[Route("api/cash-boxes")]
public class CashBoxesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var data = await Mediator.Send(new GetCashBoxesQuery(activeOnly));
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await Mediator.Send(new GetCashBoxByIdQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "الصندوق غير موجود" });
        return Ok(new { success = true, data = dto });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCashBoxDto body)
    {
        try
        {
            var id = await Mediator.Send(new CreateCashBoxCommand(body));
            return Ok(new { success = true, data = new { id } });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertCashBoxDto body)
    {
        try
        {
            await Mediator.Send(new UpdateCashBoxCommand(id, body));
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] ToggleCashBoxRequest body)
    {
        try
        {
            await Mediator.Send(new ToggleCashBoxCommand(id, body.IsActive));
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}/move")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveCashBoxRequest body)
    {
        try
        {
            await Mediator.Send(new MoveCashBoxCommand(id, body.Direction));
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await Mediator.Send(new DeleteCashBoxCommand(id));
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public record ToggleCashBoxRequest(bool IsActive);
public record MoveCashBoxRequest(string Direction);
