using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTree;

public record GetAccountsTreeQuery() : IRequest<List<AccountDto>>;
