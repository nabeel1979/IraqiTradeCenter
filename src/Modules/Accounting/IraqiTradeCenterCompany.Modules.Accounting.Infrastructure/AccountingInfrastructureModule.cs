using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure;

public static class AccountingInfrastructureModule
{
    public static IServiceCollection AddAccountingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AccountingDbContext>(opt =>
            opt.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsHistoryTable("__EFMigrations_Accounting", AccountingDbContext.Schema)));

        services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<AccountingDbContext>());
        services.AddScoped<IPeriodResolver, PeriodResolver>();
        services.AddScoped<IAccountingService, AccountingService>();  // PUBLIC contract

        return services;
    }
}
