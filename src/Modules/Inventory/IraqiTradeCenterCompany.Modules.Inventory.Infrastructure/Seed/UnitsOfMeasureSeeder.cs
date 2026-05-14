using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Seed;

public static class UnitsOfMeasureSeeder
{
    public static async Task SeedAsync(InventoryDbContext db)
    {
        if (await db.UnitsOfMeasure.AnyAsync()) return;
        var units = new[]
        {
            UnitOfMeasure.Create("حبة", "PIECE", "Piece"),
            UnitOfMeasure.Create("علبة", "PACK", "Pack"),
            UnitOfMeasure.Create("كرتون", "BOX", "Box"),
            UnitOfMeasure.Create("بالة", "BALE", "Bale"),
            UnitOfMeasure.Create("كيلو", "KG", "Kilogram"),
            UnitOfMeasure.Create("طن", "TON", "Ton"),
            UnitOfMeasure.Create("لتر", "L", "Liter"),
            UnitOfMeasure.Create("متر", "M", "Meter"),
        };
        await db.UnitsOfMeasure.AddRangeAsync(units);
        await db.SaveChangesAsync();
    }
}
