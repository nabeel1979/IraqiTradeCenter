using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application;

/// <summary>تسجيل خدمات Application لمودول المحاسبة</summary>
public static class AccountingModule
{
    public static IServiceCollection AddAccountingApplication(this IServiceCollection services)
    {
        var assembly = typeof(AccountingModule).Assembly;
        services.AddAutoMapper(assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        return services;
    }
}
