using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Seed;

public static class FiscalYearSeeder
{
    public static async Task SeedAsync(AccountingDbContext db)
    {
        var year = DateTime.UtcNow.Year;
        if (await db.FiscalYears.AnyAsync(fy => fy.StartDate.Year == year)) return;
        var fy = FiscalYear.Create($"السنة المالية {year}",
            new DateTime(year, 1, 1), new DateTime(year, 12, 31));
        await db.FiscalYears.AddAsync(fy);
        await db.SaveChangesAsync();
    }
}
