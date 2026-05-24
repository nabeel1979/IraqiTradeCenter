using IraqiTradeCenterCompany.API.Trash.Providers;

namespace IraqiTradeCenterCompany.API.Trash;

public static class TrashExtensions
{
    /// <summary>
    /// تسجيل خدمة سلة المهملات الموحَّدة + كل مُزوِّدي الكيانات. مكان واحد لإضافة
    /// مُزوِّدي أنواع جديدة لاحقاً (Customer, Item, …) كي يظهروا في السلة الموحَّدة.
    /// </summary>
    public static IServiceCollection AddUnifiedTrash(this IServiceCollection services)
    {
        services.AddScoped<ITrashService, TrashService>();

        services.AddScoped<ITrashProvider, AccountTrashProvider>();
        services.AddScoped<ITrashProvider, CashBoxTrashProvider>();
        services.AddScoped<ITrashProvider, CashBoxTransferTrashProvider>();
        services.AddScoped<ITrashProvider, JournalEntryTrashProvider>();
        services.AddScoped<ITrashProvider, JournalVoucherTypeTrashProvider>();
        services.AddScoped<ITrashProvider, FiscalYearTrashProvider>();
        services.AddScoped<ITrashProvider, CurrencyRateBulletinTrashProvider>();

        return services;
    }
}
