using FluentValidation;

namespace IraqiTradeCenterCompany.Modules.Inventory.Application.Features.CreateItem;

public class CreateItemValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(300);
        RuleFor(x => x.BaseUnitId).GreaterThan(0);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BaseSalesPrice).GreaterThanOrEqualTo(x => x.PurchasePrice);
    }
}
