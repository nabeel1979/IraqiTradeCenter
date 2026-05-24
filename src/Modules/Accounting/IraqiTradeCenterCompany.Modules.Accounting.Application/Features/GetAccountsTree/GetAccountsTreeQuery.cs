using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;

/// <summary>
/// استعلام شجرة الحسابات.
/// </summary>
/// <param name="IncludeInactive">
/// إن كانت <c>true</c> فستضمَّن الحسابات المعطَّلة (IsActive=false) ضمن الشجرة
/// مع تعليمها بـ <c>IsActive=false</c> في الـ DTO. تستخدمها شاشة الإدارة فقط؛
/// أما شاشات الاختيار (قيود/صناديق/سندات) فتبقى على القيمة الافتراضية <c>false</c>.
/// </param>
public record GetAccountsTreeQuery(bool IncludeInactive = false) : IRequest<List<AccountDto>>;
