using FluentValidation;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.AddSalesRep;

public class AddSalesRepValidator : AbstractValidator<AddSalesRepCommand>
{
    public AddSalesRepValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty();
        RuleFor(x => x.Phone).Matches(@"^07[0-9]{9}$").WithMessage("رقم الهاتف غير صحيح");
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0);
        When(x => x.CommissionType == CommissionType.Fixed, () =>
            RuleFor(x => x.FixedCommissionRate).NotNull().InclusiveBetween(0, 100));
        When(x => x.CommissionType == CommissionType.Tiered, () =>
            RuleFor(x => x.Tiers).NotEmpty().WithMessage("الشرائح مطلوبة للعمولة المتدرجة"));
    }
}
