using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountStatement;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTrash;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetJournalEntriesList;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountBalances;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageJournalEntry;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

public class AccountsController : BaseApiController
{
    private readonly AuthDbContext _authDb;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _currentUser;

    public AccountsController(AuthDbContext authDb, IPermissionService permissions, ICurrentUserService currentUser)
    {
        _authDb = authDb;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    /// <summary>
    /// يُعيد قائمة معرّفات الصناديق المسموحة للمستخدم الحالي إن وَجَب تطبيق
    /// فلتر تقارير الصناديق، أو <c>null</c> لتجاوز الفلتر (SuperAdmin أو من
    /// لديه <c>Accounting.CashBoxes.ViewAll</c>).
    /// </summary>
    private async Task<IReadOnlyCollection<int>?> ResolveCashBoxScopeAsync(CancellationToken ct)
    {
        if (_currentUser.IsSuperAdmin) return null;
        var uid = _currentUser.UserId;
        if (uid is null) return null; // anonymous — تُلتقط بطبقة المصادقة قبل أن نصل هنا
        if (_currentUser.HasPermission(PermissionRegistry.Accounting.CashBoxes.ViewAll)) return null;
        return await _permissions.GetUserCashBoxIdsAsync(uid.Value, ct);
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree([FromQuery] bool includeInactive = false)
    {
        var tree = await Mediator.Send(new GetAccountsTreeQuery(includeInactive));
        return Ok(new { success = true, data = tree });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAccountCommand body)
    {
        var cmd = body with { Id = id };
        return HandleResult(await Mediator.Send(cmd));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
        => HandleResult(await Mediator.Send(new DeleteAccountCommand(id)));

    // ─────────────────────────────────────────────────────────────
    // سلة المهملات: قائمة + استعادة + حذف نهائي
    // ─────────────────────────────────────────────────────────────

    /// <summary>قائمة الحسابات الموجودة في سلة المهملات (محذوفة ناعماً).</summary>
    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash()
    {
        var data = await Mediator.Send(new GetAccountsTrashQuery());
        return Ok(new { success = true, data });
    }

    /// <summary>استعادة حساب من سلة المهملات (يعكس الحذف الناعم).</summary>
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
        => HandleResult(await Mediator.Send(new RestoreAccountCommand(id)));

    /// <summary>حذف نهائي للحساب من قاعدة البيانات. مسموح فقط من سلة المهملات.</summary>
    [HttpDelete("{id:int}/permanent")]
    public async Task<IActionResult> PermanentlyDelete(int id)
        => HandleResult(await Mediator.Send(new PermanentlyDeleteAccountCommand(id)));

    /// <summary>تفصيل استخدام الحساب — لشرح للمستخدم لماذا لا يمكن إضافة فرع/حذف.</summary>
    [HttpGet("{id:int}/usage")]
    public async Task<IActionResult> GetUsage(int id)
    {
        var data = await Mediator.Send(new GetAccountUsageQuery(id));
        return Ok(new { success = true, data });
    }

    [HttpGet("journal-entries")]
    public async Task<IActionResult> GetJournalEntries([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] int? voucherTypeId = null,
        [FromQuery] bool excludeSidebarVoucherTypes = false,
        CancellationToken ct = default)
    {
        var allowed = await ResolveCashBoxScopeAsync(ct);
        var data = await Mediator.Send(new GetJournalEntriesListQuery(
            pageNumber, pageSize, status, search, fromDate, toDate, voucherTypeId, excludeSidebarVoucherTypes, allowed), ct);
        return Ok(new { success = true, data });
    }

    [HttpPost("journal-entries")]
    public async Task<IActionResult> PostJournalEntry([FromBody] PostJournalEntryCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpGet("journal-entries/{id:int}")]
    public async Task<IActionResult> GetJournalEntryById(int id)
    {
        var dto = await Mediator.Send(new GetJournalEntryByIdQuery(id));
        if (dto == null) return NotFound(new { success = false, message = "القيد غير موجود" });
        return Ok(new { success = true, data = dto });
    }

    [HttpPut("journal-entries/{id:int}")]
    public async Task<IActionResult> UpdateJournalEntry(int id, [FromBody] UpdateJournalEntryCommand body)
    {
        var cmd = body with { Id = id };
        return HandleResult(await Mediator.Send(cmd));
    }

    [HttpDelete("journal-entries/{id:int}")]
    public async Task<IActionResult> DeleteJournalEntry(int id)
        => HandleResult(await Mediator.Send(new DeleteJournalEntryCommand(id)));

    // ─────────────────────────────────────────────────────────────
    // تعديل/حذف سند مخصّص (سند قبض/دفع/…). نقاط نهاية منفصلة عن القيود
    // العادية لأن الـ Update/Delete العاديين يرفضان القيود المُدارة.
    // ─────────────────────────────────────────────────────────────
    [HttpPut("vouchers/{id:int}")]
    public async Task<IActionResult> UpdateVoucherEntry(int id, [FromBody] UpdateVoucherEntryCommand body)
    {
        var cmd = body with { Id = id };
        return HandleResult(await Mediator.Send(cmd));
    }

    [HttpDelete("vouchers/{id:int}")]
    public async Task<IActionResult> DeleteVoucherEntry(int id)
        => HandleResult(await Mediator.Send(new DeleteVoucherEntryCommand(id)));

    [HttpGet("balances")]
    public async Task<IActionResult> GetAccountBalances(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? accountId = null,
        [FromQuery] string? currency = null,
        [FromQuery] bool valuated = false,
        [FromQuery] int? maxLevel = null,
        [FromQuery] bool leavesOnly = true,
        [FromQuery] bool includeDraft = false)
    {
        var data = await Mediator.Send(new GetAccountBalancesQuery(
            from, to, accountId, currency, valuated, maxLevel, leavesOnly, includeDraft));
        return Ok(new { success = true, data });
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? currency = null,
        [FromQuery] bool valuated = false,
        [FromQuery] int? maxLevel = null,
        [FromQuery] bool leavesOnly = true,
        [FromQuery] bool includeDraft = false)
    {
        var data = await Mediator.Send(new GetTrialBalanceQuery(
            from, to, currency, valuated, maxLevel, leavesOnly, includeDraft));
        return Ok(new { success = true, data });
    }

    [HttpGet("statement")]
    public async Task<IActionResult> GetAccountStatement(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int? accountId = null,
        [FromQuery] string? currency = null,
        [FromQuery] bool includeDraft = false)
    {
        var s = await _authDb.CompanySettings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        var baseCur = string.IsNullOrWhiteSpace(s?.Currency) ? "IQD" : s!.Currency!.Trim().ToUpperInvariant();
        var fxJson = s?.ExchangeRatesJson;

        var data = await Mediator.Send(new GetAccountStatementQuery(
            from, to, accountId, currency, includeDraft,
            BaseCurrency: baseCur,
            ExchangeRatesJson: fxJson));

        return Ok(new { success = true, data });
    }
}
