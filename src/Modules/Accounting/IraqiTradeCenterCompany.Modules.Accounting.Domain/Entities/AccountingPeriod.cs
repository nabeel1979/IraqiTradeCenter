using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class AccountingPeriod : BaseEntity
{
    public int FiscalYearId { get; private set; }
    public int PeriodNumber { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public PeriodStatus Status { get; private set; }

    private AccountingPeriod() { }

    internal static AccountingPeriod Create(int num, DateTime start, DateTime end)
        => new() { PeriodNumber = num, StartDate = start, EndDate = end, Status = PeriodStatus.Open };

    public bool ContainsDate(DateTime d) => d.Date >= StartDate.Date && d.Date <= EndDate.Date;
    public void Close() => Status = PeriodStatus.Closed;
    public void Lock() => Status = PeriodStatus.Locked;
    public void Reopen() { if (Status != PeriodStatus.Locked) Status = PeriodStatus.Open; }
}
