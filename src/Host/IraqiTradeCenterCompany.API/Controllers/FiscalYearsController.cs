using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.FiscalYearManagement;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>السنة المالية المفعَّلة. تُرجع null إن لم تكن هناك سنة نشطة.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var data = await Mediator.Send(new GetActiveFiscalYearQuery());
        return Ok(new { success = true, data });
    }

    /// <summary>تفعيل سنة مالية كنشطة (وتعطيل بقية السنوات تلقائياً).</summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id)
    {
        try
        {
            await Mediator.Send(new ActivateFiscalYearCommand(id));
            return Ok(new { success = true, message = "تم تفعيل السنة المالية" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
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

    public record CreateFiscalYearBody(string Name, DateTime StartDate, DateTime EndDate, string? NameEn = null);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFiscalYearBody body)
    {
        try
        {
            var id = await Mediator.Send(new CreateFiscalYearCommand(body.Name, body.StartDate, body.EndDate, body.NameEn));
            return Ok(new { success = true, data = id, message = "تم إنشاء السنة المالية بنجاح" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
        {
            // ‎فهرس فريد منتهَك → نحاول استخراج اسم القيد لإعطاء رسالة واضحة.
            var msg = sql.Number switch
            {
                2601 or 2627 => "البيانات تتعارض مع قيد فريد في قاعدة البيانات (اسم/تاريخ مكرر).",
                _ => $"فشل الحفظ: {sql.Message}",
            };
            return BadRequest(new { success = false, message = msg });
        }
    }

    public record UpdateFiscalYearBody(string Name, DateTime StartDate, DateTime EndDate, string? NameEn = null);

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFiscalYearBody body)
    {
        try
        {
            await Mediator.Send(new UpdateFiscalYearCommand(id, body.Name, body.StartDate, body.EndDate, body.NameEn));
            return Ok(new { success = true, message = "تم تحديث السنة المالية بنجاح" });
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
            await Mediator.Send(new DeleteFiscalYearCommand(id));
            return Ok(new { success = true, message = "تم حذف السنة المالية بنجاح" });
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

    /// <summary>
    /// فك إغلاق سنة مالية مغلقة وإعادة فتح جميع فتراتها.
    /// </summary>
    [HttpPost("{id:int}/reopen")]
    public async Task<IActionResult> Reopen(int id)
    {
        try
        {
            await Mediator.Send(new ReopenFiscalYearCommand(id));
            return Ok(new { success = true, message = "تم فك إغلاق السنة المالية بنجاح" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // إدارة الفترات المحاسبية الفردية
    // ════════════════════════════════════════════════════════════════════════

    public record UpdatePeriodBody(DateTime StartDate, DateTime EndDate);

    [HttpPut("periods/{periodId:int}")]
    public async Task<IActionResult> UpdatePeriod(int periodId, [FromBody] UpdatePeriodBody body)
    {
        try
        {
            await Mediator.Send(new UpdateAccountingPeriodCommand(periodId, body.StartDate, body.EndDate));
            return Ok(new { success = true, message = "تم تحديث الفترة بنجاح" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpDelete("periods/{periodId:int}")]
    public async Task<IActionResult> DeletePeriod(int periodId)
    {
        try
        {
            await Mediator.Send(new DeleteAccountingPeriodCommand(periodId));
            return Ok(new { success = true, message = "تم حذف الفترة بنجاح" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    public record SetPeriodStatusBody(int Status);

    /// <summary>تغيير حالة الفترة الفردية: 1=Open، 2=Closed، 3=Locked.</summary>
    [HttpPost("periods/{periodId:int}/status")]
    public async Task<IActionResult> SetPeriodStatus(int periodId, [FromBody] SetPeriodStatusBody body)
    {
        try
        {
            await Mediator.Send(new SetAccountingPeriodStatusCommand(periodId, body.Status));
            return Ok(new { success = true, message = "تم تحديث حالة الفترة" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// استعلام حالة الفترة المحاسبية المرتبطة بتاريخ معيّن — يستدعيها الـ frontend
    /// قبل عرض أزرار التعديل/الحذف على القيود لمعرفة هل التاريخ ضمن فترة مفتوحة.
    /// </summary>
    [HttpGet("period-status")]
    public async Task<IActionResult> GetPeriodStatusByDate([FromQuery] DateTime date)
    {
        var dto = await Mediator.Send(new GetPeriodStatusByDateQuery(date));
        if (dto == null)
            return Ok(new { success = false, message = $"لا توجد فترة محاسبية للتاريخ {date:yyyy-MM-dd}" });
        return Ok(new { success = true, data = dto });
    }

    /// <summary>
    /// إغلاق/فتح الفترات بالجملة لسنة مالية بناءً على تاريخ. مفيدة لإغلاق
    /// كل الأشهر حتى نهاية الربع/النصف، أو فتح ما بعد تاريخ معيّن.
    /// Mode: 1=CloseUpTo (إغلاق كل ما EndDate ≤ Date)
    ///       2=OpenFrom  (فتح كل ما StartDate ≥ Date)
    /// TargetStatus: 1=Open، 2=Closed، 3=Locked
    /// </summary>
    public record BulkPeriodsBody(int FiscalYearId, DateTime Date, int Mode, int TargetStatus);

    [HttpPost("periods/bulk-status")]
    public async Task<IActionResult> BulkSetPeriodsStatus([FromBody] BulkPeriodsBody body)
    {
        try
        {
            var dto = await Mediator.Send(new BulkSetPeriodsStatusCommand(
                body.FiscalYearId,
                body.Date,
                (BulkPeriodMode)body.Mode,
                body.TargetStatus));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// إعادة مزامنة الفترات الشهرية لتطابق تواريخ السنة المالية الحالية.
    /// مفيدة لإصلاح سنة عُدّلت تواريخها سابقاً وبقيت فترات معلّقة خارج نطاقها.
    /// </summary>
    [HttpPost("{id:int}/resync-periods")]
    public async Task<IActionResult> ResyncPeriods(int id)
    {
        try
        {
            var dto = await Mediator.Send(new ResyncFiscalYearPeriodsCommand(id));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Mode: 1=WithProfitLoss، 2=BalanceSheetOnly، 3=AllAccounts
    /// </summary>
    public record RolloverBody(
        int SourceFiscalYearId,
        int TargetFiscalYearId,
        string? ProfitAccountCode,
        string? LossAccountCode,
        int Mode = 1,
        bool PreviewOnly = false,
        DateTime? OpeningEntryDate = null
    );

    [HttpPost("rollover")]
    public async Task<IActionResult> Rollover([FromBody] RolloverBody body)
    {
        try
        {
            var by = User.Identity?.Name ?? "system";
            var dto = await Mediator.Send(new RolloverFiscalYearCommand(
                SourceFiscalYearId: body.SourceFiscalYearId,
                TargetFiscalYearId: body.TargetFiscalYearId,
                PerformedBy: by,
                ProfitAccountCode: body.ProfitAccountCode,
                LossAccountCode: body.LossAccountCode,
                Mode: (RolloverMode)body.Mode,
                PreviewOnly: body.PreviewOnly,
                OpeningEntryDate: body.OpeningEntryDate));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// التراجع عن تدوير الأرصدة: حذف القيد الافتتاحي في السنة الهدف، تصفير
    /// OpeningBalance للحسابات المتأثّرة، ويفك إغلاق السنة السابقة (اختيارياً).
    /// </summary>
    public record UndoRolloverBody(int TargetFiscalYearId, bool ReopenSource = true);

    [HttpPost("undo-rollover")]
    public async Task<IActionResult> UndoRollover([FromBody] UndoRolloverBody body)
    {
        try
        {
            var dto = await Mediator.Send(new UndoRolloverCommand(body.TargetFiscalYearId, body.ReopenSource));
            return Ok(new { success = true, data = dto, message = dto.Message });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }
}
