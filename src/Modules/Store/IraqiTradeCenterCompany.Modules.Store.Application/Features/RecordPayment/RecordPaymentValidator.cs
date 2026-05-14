using FluentValidation;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.RecordPayment;

public class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.SalesInvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).NotEmpty();
    }
}
