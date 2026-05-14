using IraqiTradeCenterCompany.Modules.Inventory.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.RecordStockMovement;

public record RecordStockMovementCommand(
    int ItemId, int WarehouseId, StockMovementType Type,
    int UnitOfMeasureId, decimal Quantity,
    decimal? UnitCost, string? ReferenceNumber, string? Notes
) : IRequest<Result<int>>;
