using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public record DeleteAccountCommand(int Id) : IRequest<Result>;
