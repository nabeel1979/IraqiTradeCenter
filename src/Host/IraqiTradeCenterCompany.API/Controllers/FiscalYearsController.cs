using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

[Route("api/fiscal-years")]
public class FiscalYearsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await Mediator.Send(new GetFiscalYearsQuery());
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}/status")]
    public async Task<IActionResult> GetStatus(int id)
    {
        var dto = await Mediator.Send(new GetFiscalYearStatusQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "السنة المالية غير موجودة" });
        return Ok(new { success = true, data = dto });
    }

    [HttpGet("{id:int}/validate")]
    public async Task<IActionResult> Validate(int id)
    {
        var dto = await Mediator.Send(new ValidateFiscalYearQuery(id));
        return Ok(new { success = true, data = dto });
    }

    public record CreateFiscalYearBody(string Name, DateTime StartDate, DateTime EndDate);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFiscalYearBody body)
    {
        try
        {
            var id = await Mediator.Send(new CreateFiscalYearCommand(body.Name, body.StartDate, body.EndDate));
            return Ok(new { success = true, data = id, message = "تم إنشاء السنة المالية بنجاح" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    public record CloseFiscalYearBody(bool ForceClose = false);

    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id, [FromBody] CloseFiscalYearBody? body)
    {
        try
        {
            var closedBy = User.Identity?.Name ?? "system";
            var force = body?.ForceClose ?? false;
            var dto = await Mediator.Send(new CloseFiscalYearCommand(id, closedBy, force));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    public record RolloverBody(
        int SourceFiscalYearId,
        int TargetFiscalYearId,
        string RetainedEarningsCode,
        bool PreviewOnly = false
    );

    [HttpPost("rollover")]
    public async Task<IActionResult> Rollover([FromBody] RolloverBody body)
    {
        try
        {
            var by = User.Identity?.Name ?? "system";
            var dto = await Mediator.Send(new RolloverFiscalYearCommand(
                body.SourceFiscalYearId, body.TargetFiscalYearId, by,
                body.RetainedEarningsCode, body.PreviewOnly));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
