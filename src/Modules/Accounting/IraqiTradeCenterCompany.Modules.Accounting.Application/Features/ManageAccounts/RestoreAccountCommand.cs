using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

/// <summary>
/// استعادة حساب من سلة المهملات (يعكس الحذف الناعم).
/// شروط:
///   • الحساب موجود فعلاً ومحذوف ناعماً (IsDeleted=true)
///   • أبوه (إن وُجد) ليس محذوفاً — وإلا فلا يوجد له موضع في الشجرة بعد الاستعادة
///   • لا توجد كيان آخر بنفس الكود مفعَّلاً (شرط نظري لأن الفهرس الفريد يضمنه)
/// </summary>
public record RestoreAccountCommand(int Id) : IRequest<Result>;
