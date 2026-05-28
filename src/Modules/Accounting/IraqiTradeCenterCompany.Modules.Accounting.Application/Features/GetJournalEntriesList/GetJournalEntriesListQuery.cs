using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetJournalEntriesList;

public record GetJournalEntriesListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? SearchTerm = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? VoucherTypeId = null,
    /// <summary>
    /// عند true: استبعد القيود التي نوع سندها مفعَّل في القائمة الجانبية (ShowInSidebar=true).
    /// مفيد لصفحة "القيود اليومية" التي تعرض فقط القيود غير المُسندة إلى أنواع سندات لها صفحات مخصّصة.
    /// </summary>
    bool ExcludeSidebarVoucherTypes = false,
    /// <summary>
    /// إن لم تكن <c>null</c> فالنتائج تُحصَر بالقيود التي تتضمّن سطراً مربوطاً
    /// بأحد هذه الصناديق (عبر <c>JournalEntryLine.AccountId == CashBox.AccountId</c>).
    /// تُمرَّر من الـ Controller بناءً على صلاحيات المستخدم. <c>null</c> = لا فلتر.
    /// </summary>
    IReadOnlyCollection<int>? AllowedCashBoxIds = null
) : IRequest<PagedResult<JournalEntryDto>>;
