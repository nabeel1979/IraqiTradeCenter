using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class FiscalYear : BaseEntity
{
    public string Name { get; private set; } = default!;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public virtual ICollection<AccountingPeriod> Periods { get; private set; } = new List<AccountingPeriod>();

    private FiscalYear() { }

    public static FiscalYear Create(string name, DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate) throw new DomainException("تاريخ النهاية يجب أن يكون بعد البداية");
        var fy = new FiscalYear { Name = name, StartDate = startDate, EndDate = endDate };
        var current = startDate; int n = 1;
        while (current < endDate)
        {
            var pEnd = current.AddMonths(1).AddDays(-1);
            if (pEnd > endDate) pEnd = endDate;
            fy.Periods.Add(AccountingPeriod.Create(n++, current, pEnd));
            current = current.AddMonths(1);
        }
        return fy;
    }

    public void Close() { IsClosed = true; ClosedAt = DateTime.UtcNow; }
}
