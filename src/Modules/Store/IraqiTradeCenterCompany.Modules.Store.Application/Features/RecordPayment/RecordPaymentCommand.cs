using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.RecordPayment;

public record RecordPaymentCommand(
    int SalesInvoiceId, decimal Amount, string PaymentMethod, string? ReferenceNumber, string? Notes
) : IRequest<Result<int>>;
