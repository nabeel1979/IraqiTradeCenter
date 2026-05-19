using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

[Route("api/currency-rate-bulletins")]
public class CurrencyRateBulletinsController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? status, [FromQuery] bool includeArchived = false)
    {
        var data = await Mediator.Send(new GetCurrencyRateBulletinsQuery(status, includeArchived));
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await Mediator.Send(new GetCurrencyRateBulletinByIdQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "النشرة غير موجودة" });
        return Ok(new { success = true, data = dto });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive([FromQuery] DateTime? at)
    {
        var dto = await Mediator.Send(new GetActiveCurrencyRateBulletinQuery(at));
        return Ok(new { success = true, data = dto });
    }

    public record CreateBulletinBody(
        string Name,
        string BaseCurrency,
        DateTime EffectiveAt,
        string? Notes,
        bool PublishImmediately,
        List<CurrencyRateLinePayload> Lines);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBulletinBody body)
    {
        var r = await Mediator.Send(new CreateCurrencyRateBulletinCommand(
            body.Name, body.BaseCurrency, body.EffectiveAt, body.Notes,
            body.PublishImmediately, body.Lines ?? new()));
        return r.IsSuccess
            ? Ok(new { success = true, data = r.Value, message = "تم إنشاء النشرة بنجاح" })
            : BadRequest(new { success = false, message = r.Error });
    }

    public record UpdateBulletinBody(
        string Name,
        string BaseCurrency,
        DateTime EffectiveAt,
        string? Notes,
        List<CurrencyRateLinePayload> Lines);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBulletinBody body)
    {
        var r = await Mediator.Send(new UpdateCurrencyRateBulletinCommand(
            id, body.Name, body.BaseCurrency, body.EffectiveAt, body.Notes, body.Lines ?? new()));
        return r.IsSuccess
            ? Ok(new { success = true, message = "تم التحديث بنجاح" })
            : BadRequest(new { success = false, message = r.Error });
    }

    [HttpPost("{id:int}/publish")]
    public async Task<IActionResult> Publish(int id)
    {
        var r = await Mediator.Send(new PublishCurrencyRateBulletinCommand(id));
        return r.IsSuccess
            ? Ok(new { success = true, message = "تم نشر النشرة" })
            : BadRequest(new { success = false, message = r.Error });
    }

    [HttpPost("{id:int}/archive")]
    public async Task<IActionResult> Archive(int id)
    {
        var r = await Mediator.Send(new ArchiveCurrencyRateBulletinCommand(id));
        return r.IsSuccess
            ? Ok(new { success = true, message = "تم أرشفة النشرة" })
            : BadRequest(new { success = false, message = r.Error });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await Mediator.Send(new DeleteCurrencyRateBulletinCommand(id));
        return r.IsSuccess
            ? Ok(new { success = true, message = "تم الحذف" })
            : BadRequest(new { success = false, message = r.Error });
    }
}
