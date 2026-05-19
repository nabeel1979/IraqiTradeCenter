using FluentValidation;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;

public class PostJournalEntryValidator : AbstractValidator<PostJournalEntryCommand>
{
    public PostJournalEntryValidator()
    {
        // البيان اختياري - عند تركه فارغاً يُحفظ "—"
        RuleFor(x => x.Lines).NotEmpty().Must(l => l.Count >= 2)
            .WithMessage("القيد لازم سطرين على الأقل");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).GreaterThan(0);
            line.RuleFor(l => l.Amount).GreaterThan(0);
        });
        RuleFor(x => x.Lines).Must(lines =>
        {
            var d = lines.Where(l => l.IsDebit).Sum(l => l.Amount);
            var c = lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
            return Math.Round(d, 3) == Math.Round(c, 3);
        }).WithMessage("القيد غير متوازن");
    }
}
