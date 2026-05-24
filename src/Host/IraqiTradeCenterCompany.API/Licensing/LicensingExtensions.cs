using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IraqiTradeCenterCompany.API.Licensing;

public static class LicensingExtensions
{
    /// <summary>تسجيل خدمات الترخيص والمحفظة في DI.</summary>
    public static IServiceCollection AddSystemLicensing(this IServiceCollection services)
    {
        services.TryAddSingleton<ILicenseService, LicenseService>();
        services.TryAddSingleton<IWalletService,  WalletService>();
        return services;
    }
}
