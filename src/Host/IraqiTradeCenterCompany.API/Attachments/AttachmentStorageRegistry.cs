using System;
using System.Threading;
using System.Threading.Tasks;
using IraqiTradeCenterCompany.API.Settings;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;

namespace IraqiTradeCenterCompany.API.Attachments;

/// <summary>
/// سجل مزوّدي تخزين المرفقات: يُتيح للـ controllers انتقاء التطبيق المناسب
/// بحسب اسم المزوّد (<c>"Local"</c> / <c>"R2"</c>) المحفوظ على كل سطر مرفق.
/// هذا ضروري لأن الإعداد الحالي قد يكون R2 لكن ملفّاً قديماً مازال موجوداً
/// محلياً — يجب قراءته بنفس المخزن الذي حُفظ به.
/// </summary>
public interface IAttachmentStorageRegistry
{
    /// <summary>التخزين المُعتمد حالياً (بحسب إعدادات قاعدة البيانات).</summary>
    Task<IAttachmentStorage> CurrentAsync(CancellationToken ct = default);

    /// <summary>اسم المزوّد الحالي ("Local" / "R2") — مُريح للحفظ في عمود الـ provider.</summary>
    Task<string> CurrentProviderNameAsync(CancellationToken ct = default);

    /// <summary>الحصول على التطبيق المسجّل تحت اسم محدّد (يسقط افتراضياً إلى Local).</summary>
    IAttachmentStorage GetByName(string providerName);
}

public class AttachmentStorageRegistry : IAttachmentStorageRegistry
{
    private readonly IAttachmentSettingsService _settings;
    private readonly LocalDiskAttachmentStorage _local;
    private readonly R2AttachmentStorage _r2;

    public AttachmentStorageRegistry(
        IAttachmentSettingsService settings,
        LocalDiskAttachmentStorage local,
        R2AttachmentStorage r2)
    {
        _settings = settings;
        _local = local;
        _r2 = r2;
    }

    public async Task<IAttachmentStorage> CurrentAsync(CancellationToken ct = default)
    {
        var row = await _settings.GetAsync(ct);
        return string.Equals(row.Provider, "R2", StringComparison.OrdinalIgnoreCase) ? _r2 : _local;
    }

    public async Task<string> CurrentProviderNameAsync(CancellationToken ct = default)
        => (await CurrentAsync(ct)).ProviderName;

    public IAttachmentStorage GetByName(string providerName)
    {
        if (string.Equals(providerName, "R2", StringComparison.OrdinalIgnoreCase)) return _r2;
        return _local;
    }
}
