using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Seed;

public static class DefaultWarehouseSeeder
{
    public static async Task SeedAsync(InventoryDbContext db)
    {
        if (await db.Warehouses.AnyAsync()) return;
        await db.Warehouses.AddAsync(Warehouse.Create("MAIN", "المخزن الرئيسي", isDefault: true));
        await db.SaveChangesAsync();
    }
}
