using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class AccountsController : BaseApiController
{
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree()
    {
        var tree = await Mediator.Send(new GetAccountsTreeQuery());
        return Ok(new { success = true, data = tree });
    }

    [HttpPost("journal-entries")]
    public async Task<IActionResult> PostJournalEntry([FromBody] PostJournalEntryCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var data = await Mediator.Send(new GetTrialBalanceQuery(from, to));
        return Ok(new { success = true, data });
    }
}
