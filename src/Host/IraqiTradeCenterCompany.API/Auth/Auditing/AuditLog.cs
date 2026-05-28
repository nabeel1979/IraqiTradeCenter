using System;

namespace IraqiTradeCenterCompany.API.Auth.Auditing;

/// <summary>
/// سجل عمليات: يلتقط كل عملية حسّاسة يقوم بها مستخدم على الكيانات الأساسية
/// (قيود محاسبية، سندات قبض/دفع، مناقلات، حسابات، صناديق، إعدادات…).
/// الجدول append-only — لا يُعدَّل سجل بعد كتابته. الفهارس مصمَّمة على:
///   - <see cref="EntityType"/> + <see cref="EntityId"/> لاستعراض تاريخ كيان واحد.
///   - <see cref="OccurredAtUtc"/> لاستعراض الجدول الزمني.
///   - <see cref="UserId"/> لتقارير "ماذا فعل المستخدم X".
/// لا تربط هذا الكيان بـ FKs خارج auth schema لأن السجلات تبقى صالحة حتى بعد
/// حذف الكيانات المرجعية (soft-delete أو غير ذلك).
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    /// <summary>اسم الكيان (مثلاً <c>"JournalEntry"</c>، <c>"Voucher"</c>، <c>"CashBox"</c>).</summary>
    public string EntityType { get; set; } = default!;

    /// <summary>مُعرّف الكيان كنص (يدعم int و GUID على حدٍّ سواء؛ نص لتفادي مفاتيح متعدّدة).</summary>
    public string EntityId { get; set; } = default!;

    /// <summary>
    /// نوع العملية: <see cref="AuditAction.Create"/> / <see cref="AuditAction.Update"/> /
    /// <see cref="AuditAction.Delete"/> / <see cref="AuditAction.Print"/> /
    /// <see cref="AuditAction.Post"/> / <see cref="AuditAction.Unpost"/>… كنص.
    /// </summary>
    public string Action { get; set; } = default!;

    /// <summary>وصف بشري قصير للعملية يُعرض في صف الجدول (مثلاً "تعديل سند قبض #PV-12").</summary>
    public string? Summary { get; set; }

    /// <summary>JSON اختياري بحقول قبل/بعد، أو الـ payload الكامل لعملية الإنشاء.</summary>
    public string? DetailsJson { get; set; }

    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>قيم ثابتة لـ <see cref="AuditLog.Action"/> ليُسهَل البحث بها على الواجهة.</summary>
public static class AuditAction
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
