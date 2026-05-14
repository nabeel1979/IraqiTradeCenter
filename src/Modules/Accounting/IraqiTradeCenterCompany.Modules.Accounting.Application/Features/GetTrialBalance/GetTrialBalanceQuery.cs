using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;

public record GetTrialBalanceQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<List<TrialBalanceRowDto>>;
