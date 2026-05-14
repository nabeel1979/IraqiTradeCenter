using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        var assembly = typeof(InventoryModule).Assembly;
        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        return services;
    }
}
