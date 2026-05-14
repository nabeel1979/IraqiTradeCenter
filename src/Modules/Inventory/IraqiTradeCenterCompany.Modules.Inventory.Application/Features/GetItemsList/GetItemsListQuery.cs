using IraqiTradeCenterCompany.Modules.Inventory.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.GetItemsList;

public record GetItemsListQuery(
    int PageNumber = 1, int PageSize = 20,
    string? SearchTerm = null, int? CategoryId = null, bool? LowStockOnly = null
) : IRequest<PagedResult<ItemDto>>;
