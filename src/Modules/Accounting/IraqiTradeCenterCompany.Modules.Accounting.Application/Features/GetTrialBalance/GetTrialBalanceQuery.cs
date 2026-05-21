using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;

/// <summary>
/// استعلام ميزان المراجعة الموسَّع: يدعم فلاتر السنة المالية/الفترة/العملة/مستوى الشجرة/الأبناء فقط،
/// ويُرجع لكل حساب أعمدة: مدين/دائن الفترة السابقة، مدين/دائن الفترة الحالية، رصيد مدين/دائن
/// — مع تقويم اختياري بالعملة الأساسية (مأخوذ من أحدث نشرة أسعار منشورة).
/// </summary>
public record GetTrialBalanceQuery(
    DateTime FromDate,
    DateTime ToDate,
    /// <summary>عملة محدّدة (مثلاً USD) — null = كل العملات</summary>
    string? Currency = null,
    /// <summary>true = إظهار المبالغ مقوّمة بالعملة الأساسية (نشرة الأسعار)</summary>
    bool Valuated = false,
    /// <summary>المستوى الأقصى من الشجرة (1..n) — null = جميع المستويات</summary>
    int? MaxLevel = null,
    /// <summary>true = حسابات الأبناء فقط (Leaves) — false = جميع الحسابات</summary>
    bool LeavesOnly = true,
    /// <summary>true = تضمين القيود غير المُرحَّلة (Draft) — افتراضي false</summary>
    bool IncludeDraft = false
) : IRequest<TrialBalanceDto>;
