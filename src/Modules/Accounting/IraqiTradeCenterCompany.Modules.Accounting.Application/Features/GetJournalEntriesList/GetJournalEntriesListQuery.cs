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
    int? VoucherTypeId = null
) : IRequest<PagedResult<JournalEntryDto>>;
