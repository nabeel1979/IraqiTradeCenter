using IraqiTradeCenterCompany.SharedKernel.Models;

namespace IraqiTradeCenterCompany.API.Trash;

public interface ITrashService
{
    Task<List<TrashItemDto>> ListAllAsync(CancellationToken ct);
    Task<Result> RestoreAsync(string entityType, int id, CancellationToken ct);
    Task<Result> PermanentlyDeleteAsync(string entityType, int id, CancellationToken ct);
    IReadOnlyList<string> SupportedEntityTypes { get; }
}

/// <summary>
/// واجهة موحَّدة لسلّة المهملات عبر النظام بأكمله — تُجمّع كل المُزوِّدين المسجَّلين
/// وتدير عمليات الاستعراض/الاستعادة/الحذف النهائي بناءً على <c>EntityType</c>.
/// </summary>
public class TrashService : ITrashService
{
    private readonly IReadOnlyDictionary<string, ITrashProvider> _providers;

    public TrashService(IEnumerable<ITrashProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.EntityType, p => p, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> SupportedEntityTypes => _providers.Keys.ToList();

    public async Task<List<TrashItemDto>> ListAllAsync(CancellationToken ct)
    {
        var results = new List<TrashItemDto>();
        foreach (var p in _providers.Values)
        {
            try
            {
                var items = await p.ListAsync(ct);
                results.AddRange(items);
            }
            catch (Exception ex)
            {
                // ‎لا نُسقط الطلب كله إن فشل مزوّد واحد — نضيف عنصراً يوضّح الخطأ
                // ‎للمستخدم بدل إخفاء النوع كاملاً، ولينتبه المسؤول إلى الخلل.
                results.Add(new TrashItemDto
                {
                    EntityType = p.EntityType,
                    EntityTypeLabel = p.EntityType,
                    Module = "خطأ",
                    Icon = "AlertTriangle",
                    EntityId = 0,
                    DisplayName = $"تعذّر تحميل سلة {p.EntityType}",
                    SubInfo = ex.Message,
                    CanRestore = false,
                    CannotRestoreReason = ex.Message,
                });
            }
        }
        // ‎الأحدث أولاً ضمن السلة الموحَّدة.
        return results.OrderByDescending(r => r.DeletedAt ?? DateTime.MinValue).ToList();
    }

    public Task<Result> RestoreAsync(string entityType, int id, CancellationToken ct)
    {
        if (!_providers.TryGetValue(entityType, out var p))
            return Task.FromResult(Result.Failure($"نوع غير معروف: {entityType}"));
        return p.RestoreAsync(id, ct);
    }

    public Task<Result> PermanentlyDeleteAsync(string entityType, int id, CancellationToken ct)
    {
        if (!_providers.TryGetValue(entityType, out var p))
            return Task.FromResult(Result.Failure($"نوع غير معروف: {entityType}"));
        return p.PermanentlyDeleteAsync(id, ct);
    }
}
