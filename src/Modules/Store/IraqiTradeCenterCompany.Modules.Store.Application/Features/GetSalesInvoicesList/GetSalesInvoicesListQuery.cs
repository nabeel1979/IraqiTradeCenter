using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetSalesInvoicesList;

public record GetSalesInvoicesListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    string? Status = null,
    int? CustomerId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<PagedResult<SalesInvoiceDto>>;
