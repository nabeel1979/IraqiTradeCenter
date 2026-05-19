using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountStatement;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetJournalEntriesList;
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

    public AccountsController(AuthDbContext authDb)
    {
        _authDb = authDb;
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var tree = await Mediator.Send(new GetAccountsTreeQuery());
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

    [HttpGet("journal-entries")]
    public async Task<IActionResult> GetJournalEntries([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] int? voucherTypeId = null)
    {
        var data = await Mediator.Send(new GetJournalEntriesListQuery(
            pageNumber, pageSize, status, search, fromDate, toDate, voucherTypeId));
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

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var data = await Mediator.Send(new GetTrialBalanceQuery(from, to));
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
