using System.Threading;
using System.Threading.Tasks;

namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

/// <summary>
/// واجه تسجيل الأحداث الحسّاسة من طبقة Application/Modules دون اعتماد
/// مباشر على Host. التطبيق الفعلي مقيم في طبقة Host (يستخدم AuthDbContext
/// وHttpContext) ويُحقن عبر DI. أي إخفاق في التسجيل لا يجب أن يُفشل العملية
/// التجارية الأصلية.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? summary = null,
        object? details = null,
        CancellationToken ct = default);
}

/// <summary>
/// أكواد العمليات المتعارَف عليها — تستخدمها كلٌّ من طبقة Application والواجهة
/// لفلترة سجل المراقبة. تبقى ثابتة كنصوص (لا enum) ليسهل تخزينها في قاعدة
/// البيانات والبحث عنها.
/// </summary>
public static class AuditActions
{
    public const string Create  = "Create";
    public const string Update  = "Update";
    public const string Delete  = "Delete";
    public const string Print   = "Print";
    public const string Post    = "Post";
    public const string Unpost  = "Unpost";
    public const string Reverse = "Reverse";
    public const string View    = "View";
    public const string Login   = "Login";
    public const string Logout  = "Logout";
}
