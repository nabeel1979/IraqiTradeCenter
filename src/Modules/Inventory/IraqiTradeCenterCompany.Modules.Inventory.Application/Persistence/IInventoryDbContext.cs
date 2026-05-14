using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;

public interface IInventoryDbContext
{
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<ItemCategory> ItemCategories { get; }
    DbSet<Item> Items { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockMovement> StockMovements { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
