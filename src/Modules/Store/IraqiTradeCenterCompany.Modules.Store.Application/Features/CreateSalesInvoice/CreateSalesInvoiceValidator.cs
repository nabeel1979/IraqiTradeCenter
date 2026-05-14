using FluentValidation;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;

public class CreateSalesInvoiceValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100);
        RuleFor(x => x.DiscountPercentage).InclusiveBetween(0, 100);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("الفاتورة لازم سطر واحد على الأقل");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.UnitOfMeasureId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.LineDiscount).GreaterThanOrEqualTo(0);
        });
    }
}
