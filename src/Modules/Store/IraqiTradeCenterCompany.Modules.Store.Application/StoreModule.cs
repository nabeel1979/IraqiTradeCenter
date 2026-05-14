using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Store.Application;

public static class StoreModule
{
    public static IServiceCollection AddStoreApplication(this IServiceCollection services)
    {
        var assembly = typeof(StoreModule).Assembly;
        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        return services;
    }
}
