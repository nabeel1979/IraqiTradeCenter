using IraqiTradeCenterCompany.API.Licensing.QiCard;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IraqiTradeCenterCompany.API.Licensing;

public static class LicensingExtensions
{
    /// <summary>تسجيل خدمات الترخيص والمحفظة وبوّابة QiCard في DI.</summary>
    public static IServiceCollection AddSystemLicensing(this IServiceCollection services, IConfiguration cfg)
    {
        services.TryAddSingleton<ILicenseService, LicenseService>();
        services.TryAddSingleton<IWalletService,  WalletService>();

        // ‎QiCard options + named HttpClient + service
        services.Configure<QiCardOptions>(cfg.GetSection(QiCardOptions.SectionName));
        services.AddHttpClient(QiCardClient.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QiCardOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                http.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds > 0 ? opts.TimeoutSeconds : 30);
        });
        services.TryAddSingleton<IQiCardClient,      QiCardClient>();
        services.TryAddSingleton<ICardPaymentService, CardPaymentService>();
        return services;
    }
}
