using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountBalances;

/// <summary>
/// تقرير أرصدة الحسابات: يُرجع الرصيد (مدين/دائن) لكل (حساب × عملة) حتى تاريخ معين.
/// يشبه ميزان المراجعة لكنه يعرض الأرصدة فقط، مع تفصيل العملة لكل حساب.
/// </summary>
public record GetAccountBalancesQuery(
    DateTime FromDate,
    DateTime ToDate,
    /// <summary>تصفية بحساب محدد (وجميع أحفاده) — null = جميع الحسابات</summary>
    int? AccountId = null,
    /// <summary>تصفية بعملة محددة — null = كل العملات</summary>
    string? Currency = null,
    /// <summary>true = إضافة عمود رصيد مقوَّم بالعملة الأساسية</summary>
    bool Valuated = false,
    /// <summary>المستوى الأقصى في الشجرة — null = بلا حد</summary>
    int? MaxLevel = null,
    /// <summary>true = أوراق الشجرة فقط</summary>
    bool LeavesOnly = true,
    /// <summary>true = تضمين القيود غير المُرحَّلة</summary>
    bool IncludeDraft = false
) : IRequest<AccountBalancesDto>;
