using IraqiTradeCenterCompany.Modules.Inventory.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.CreateItem;

public record CreateItemCommand(
    string Code, string Barcode, string NameAr, int? CategoryId,
    int BaseUnitId, decimal PurchasePrice, decimal BaseSalesPrice,
    int? MediumUnitId, decimal? MediumUnitFactor, decimal? MediumSalesPrice,
    int? LargeUnitId, decimal? LargeUnitFactor, decimal? LargeSalesPrice,
    decimal MinimumStockLevel, decimal OpeningStock
) : IRequest<Result<ItemDto>>;
