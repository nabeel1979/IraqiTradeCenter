using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure;

public static class InventoryInfrastructureModule
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrations_Inventory", InventoryDbContext.Schema)));

        services.AddScoped<IInventoryDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());
        services.AddScoped<IInventoryService, InventoryService>();  // PUBLIC contract

        return services;
    }
}
