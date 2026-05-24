using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.JournalVoucherTypes;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// إدارة أنواع السندات/القيود (سند قبض، سند دفع، سند تسوية، …) مع
/// إمكانية ربط كل نوع بحسابي مدين/دائن افتراضيين من الدليل المحاسبي.
/// كل عملية CUD/Toggle/Move تُحدِّث الصلاحيات الديناميكية المرتبطة بالنوع.
/// </summary>
[Route("api/journal-voucher-types")]
public class JournalVoucherTypesController : BaseApiController
{
    private readonly IVoucherTypePermissionsSync _permsSync;

    public JournalVoucherTypesController(IVoucherTypePermissionsSync permsSync)
    {
        _permsSync = permsSync;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? enabledOnly = null)
    {
        var data = await Mediator.Send(new GetJournalVoucherTypesQuery(enabledOnly));
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await Mediator.Send(new GetJournalVoucherTypeByIdQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "نوع السند غير موجود" });
        return Ok(new { success = true, data = dto });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertJournalVoucherTypeDto body)
    {
        try
        {
            var id = await Mediator.Send(new CreateJournalVoucherTypeCommand(body));
            await _permsSync.SyncAsync();
            return Ok(new { success = true, data = new { id } });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertJournalVoucherTypeDto body)
    {
        try
        {
            await Mediator.Send(new UpdateJournalVoucherTypeCommand(id, body));
            // ‎الاسم العربي ربما تغيّر → إعادة المزامنة لتحديث NameAr في جدول Permissions
            await _permsSync.SyncAsync();
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] ToggleVoucherTypeRequest body)
    {
        try
        {
            await Mediator.Send(new ToggleJournalVoucherTypeCommand(id, body.IsEnabled));
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPut("{id:int}/move")]
    public async Task<IActionResult> Move(int id, [FromBody] MoveVoucherTypeRequest body)
    {
        try
        {
            await Mediator.Send(new MoveJournalVoucherTypeCommand(id, body.Direction));
            // ‎ترتيب العرض تغيّر → إعادة المزامنة لتحديث DisplayOrder للصلاحيات
            await _permsSync.SyncAsync();
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
            await Mediator.Send(new DeleteJournalVoucherTypeCommand(id));
            // ‎النوع حُذف منطقياً → نزيل صلاحياته من جدول Permissions
            await _permsSync.SyncAsync();
            return Ok(new { success = true });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}

public record ToggleVoucherTypeRequest(bool IsEnabled);
public record MoveVoucherTypeRequest(string Direction);
