using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

/// <summary>
/// حذف نهائي (Hard delete) — مسح الحساب من قاعدة البيانات كلياً.
/// مسموح فقط للحسابات الموجودة فعلاً في سلة المهملات (IsDeleted=true).
/// عملية لا يمكن التراجع عنها — تستخدم لتحرير الكود نهائياً وإعادة استخدامه.
/// </summary>
public record PermanentlyDeleteAccountCommand(int Id) : IRequest<Result>;
