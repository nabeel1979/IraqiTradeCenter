using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public record CreateAccountCommand(
    string Code,
    string NameAr,
    string? NameEn,
    AccountType Type,
    AccountNature? Nature,
    int? ParentId,
    bool IsLeaf,
    string? Description
) : IRequest<Result<int>>;
