using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTrash;

/// <summary>قائمة الحسابات الموجودة في سلة المهملات (محذوفة ناعماً).</summary>
public record GetAccountsTrashQuery() : IRequest<List<TrashedAccountDto>>;
