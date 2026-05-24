using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CashBoxes;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// إدارة الصناديق (الخزائن) النقدية: لكل صندوق حساب من الدليل المحاسبي،
/// قائمة عملات مدعومة، وحدود (سقف) دائنة/مدينة لكل عملة.
///
/// الصلاحيات مقسَّمة إلى ثلاثة موارد مطابقة لتبويبات الواجهة:
///   • <c>Accounting.CashBoxes.*</c>       — تبويب "الصناديق" (إدارة الخزائن)
///   • <c>Accounting.CashBoxBalances.*</c> — تبويب "الأرصدة" (قراءة + طباعة)
///   • <c>Accounting.CashBoxTransfers.*</c>— تبويب "المناقلات" (CRUD + استلام/إلغاء)
/// </summary>
[Route("api/cash-boxes")]
public class CashBoxesController : BaseApiController
{
    // ─────────────────────────────────────────────────────────────────
    // الصناديق نفسها (تبويب "الصناديق")
    // ─────────────────────────────────────────────────────────────────
    [HttpGet]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Read)]
    public async Task<IActionResult> GetAll([FromQuery] bool? activeOnly = null)
    {
        var data = await Mediator.Send(new GetCashBoxesQuery(activeOnly));
        return Ok(new { success = true, data });
    }

    [HttpGet("{id:int}")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Read)]
    public async Task<IActionResult> GetById(int id)
    {
        var dto = await Mediator.Send(new GetCashBoxByIdQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "الصندوق غير موجود" });
        return Ok(new { success = true, data = dto });
    }

    [HttpPost]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Create)]
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
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Update)]
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
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Update)]
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
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Update)]
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
    [RequirePermission(PermissionRegistry.Accounting.CashBoxes.Delete)]
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

    // ─────────────────────────────────────────────────────────────────
    // الأرصدة + المناقلات بين الصناديق
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// أرصدة جميع الصناديق مفصَّلة حسب العملة (مدين/دائن/الرصيد + السقوف).
    /// تُحسب من سطور القيود المرحَّلة فقط.
    /// </summary>
    [HttpGet("balances")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxBalances.Read)]
    public async Task<IActionResult> GetBalances([FromQuery] string? currency = null)
    {
        var data = await Mediator.Send(new GetCashBoxBalancesQuery(currency));
        return Ok(new { success = true, data });
    }

    /// <summary>سجل المناقلات بين الصناديق (مع فلترة بالتاريخ والصندوق والعملة والحالة).</summary>
    [HttpGet("transfers")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Read)]
    public async Task<IActionResult> GetTransfers(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? cashBoxId = null,
        [FromQuery] string? currency = null,
        [FromQuery] string? status = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        var data = await Mediator.Send(new GetCashBoxTransfersQuery(
            fromDate, toDate, cashBoxId, currency, status, skip, take));
        return Ok(new { success = true, data });
    }

    /// <summary>
    /// إنشاء مناقلة بين صندوقَين: يُولِّد قيد الإرسال فقط، وتدخل المناقلة في حالة
    /// "بانتظار الاستلام" حتى موافقة أمين الصندوق المستلم (عبر POST /transfers/{id}/receive).
    /// </summary>
    [HttpPost("transfers")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Create)]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateCashBoxTransferDto body)
    {
        var result = await Mediator.Send(new CreateCashBoxTransferCommand(body));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true, data = new { id = result.Value, transferId = result.Value } });
    }

    /// <summary>
    /// تأكيد استلام مناقلة من قِبَل أمين الصندوق المستلم (يمكن أن يكون مستخدماً
    /// مختلفاً عمَّن أنشأ المناقلة). يُولِّد قيد الاستلام بتاريخ ووقت فعليَّيْن.
    /// </summary>
    [HttpPost("transfers/{id:int}/receive")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Receive)]
    public async Task<IActionResult> ReceiveTransfer(int id, [FromBody] ReceiveCashBoxTransferDto body)
    {
        var result = await Mediator.Send(new ReceiveCashBoxTransferCommand(id, body));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true, data = new { receiveJournalEntryId = result.Value } });
    }

    /// <summary>
    /// إلغاء مناقلة قبل الاستلام: يُولَّد قيد عكس للإرسال يُغلق الحساب الوسيط
    /// ويُعيد المبلغ إلى الصندوق المُرسِل.
    /// </summary>
    [HttpPost("transfers/{id:int}/cancel")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Cancel)]
    public async Task<IActionResult> CancelTransfer(int id, [FromBody] CancelCashBoxTransferDto body)
    {
        var result = await Mediator.Send(new CancelCashBoxTransferCommand(id, body));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true, data = new { reversalJournalEntryId = result.Value } });
    }

    /// <summary>
    /// التراجع عن استلام مناقلة سبق وأكَّدها أمين الصندوق المستلم. يُولَّد قيد
    /// عكس للاستلام (يُخصَم من الصندوق المستلم ويُعاد للحساب الوسيط) بشرط
    /// توفّر الرصيد. تعود المناقلة إلى حالة "بانتظار الاستلام" حتى يستطيع
    /// المُرسِل تعديلها أو إلغاءها.
    /// </summary>
    [HttpPost("transfers/{id:int}/unreceive")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Receive)]
    public async Task<IActionResult> UnreceiveTransfer(int id, [FromBody] UnreceiveCashBoxTransferDto body)
    {
        var result = await Mediator.Send(new UnreceiveCashBoxTransferCommand(id, body));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true, data = new { reversalJournalEntryId = result.Value } });
    }

    /// <summary>
    /// تعديل بيانات مناقلة بانتظار الاستلام: يُعيد توليد قيد الإرسال بالمبلغ/
    /// التاريخ/الحساب الوسيط الجديد. الصناديق والعملة لا تُعدَّل من هنا.
    /// </summary>
    [HttpPut("transfers/{id:int}")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Update)]
    public async Task<IActionResult> UpdateTransfer(int id, [FromBody] UpdateCashBoxTransferDto body)
    {
        var result = await Mediator.Send(new UpdateCashBoxTransferCommand(id, body));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true, data = new { newSendJournalEntryId = result.Value } });
    }

    /// <summary>
    /// حذف مناقلة ملغاة نهائياً مع جميع قيودها المحاسبية المرتبطة بها (الإرسال
    /// + عكس الإلغاء + قيود الاستلام/التراجع التاريخية إن وجدت). الحذف
    /// متاح فقط للمناقلات في حالة "ملغاة"؛ غير ذلك يجب إلغاؤها أوّلاً.
    /// </summary>
    [HttpDelete("transfers/{id:int}")]
    [RequirePermission(PermissionRegistry.Accounting.CashBoxTransfers.Delete)]
    public async Task<IActionResult> DeleteTransfer(int id, [FromBody] DeleteCashBoxTransferDto? body = null)
    {
        var result = await Mediator.Send(new DeleteCashBoxTransferCommand(id, body ?? new DeleteCashBoxTransferDto(null)));
        if (!result.IsSuccess)
            return BadRequest(new { success = false, message = result.Error });
        return Ok(new { success = true });
    }
}

public record ToggleCashBoxRequest(bool IsActive);
public record MoveCashBoxRequest(string Direction);
