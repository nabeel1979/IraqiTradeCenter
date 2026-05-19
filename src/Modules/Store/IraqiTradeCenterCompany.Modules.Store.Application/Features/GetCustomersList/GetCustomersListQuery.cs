using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomersList;

public record GetCustomersListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? SearchTerm = null,
    bool? ActiveOnly = true
) : IRequest<PagedResult<CustomerDto>>;
