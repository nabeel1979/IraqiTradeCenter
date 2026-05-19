using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public record UpdateAccountCommand(
    int Id,
    string NameAr,
    string? NameEn,
    AccountType Type,
    AccountNature Nature,
    string? Description,
    bool IsActive
) : IRequest<Result>;
