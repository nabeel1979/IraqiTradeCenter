using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure;

public static class StoreInfrastructureModule
{
    public static IServiceCollection AddStoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StoreDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrations_Store", StoreDbContext.Schema)));

        services.AddScoped<IStoreDbContext>(sp => sp.GetRequiredService<StoreDbContext>());
        return services;
    }
}
