using IraqiTradeCenterCompany.Modules.Inventory.Application.Features.CreateItem;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Features.GetItemsList;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Features.RecordStockMovement;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

public class ItemsController : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemCommand cmd)
        => HandleResult(await Mediator.Send(cmd));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] int? categoryId = null, [FromQuery] bool? lowStock = null)
    {
        var data = await Mediator.Send(new GetItemsListQuery(pageNumber, pageSize, search, categoryId, lowStock));
        return Ok(new { success = true, data });
    }

    [HttpPost("stock-movements")]
    public async Task<IActionResult> RecordMovement([FromBody] RecordStockMovementCommand cmd)
        => HandleResult(await Mediator.Send(cmd));
}
