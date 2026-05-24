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
    /// <summary>إعادة فتح فترة مغلقة (لا يُؤثّر على الفترات المقفلة).</summary>
    public void Reopen() { if (Status != PeriodStatus.Locked) Status = PeriodStatus.Open; }
    /// <summary>
    /// فتح قسري — حتى لو كانت الفترة مقفلة. يُستخدم عند فك إغلاق السنة المالية
    /// كاملةً ليمكن تعديل القيود فيها مجدداً.
    /// </summary>
    public void ForceOpen() => Status = PeriodStatus.Open;

    /// <summary>توسيع/تعديل تاريخ بداية الفترة (يُستخدم عند تعديل حدود السنة المالية).</summary>
    public void SetStartDate(DateTime start) => StartDate = start;
    /// <summary>توسيع/تعديل تاريخ نهاية الفترة (يُستخدم عند تعديل حدود السنة المالية).</summary>
    public void SetEndDate(DateTime end) => EndDate = end;
    /// <summary>إعادة ترقيم الفترة (يُستخدم بعد حذف فترات لإعادة الترتيب).</summary>
    public void SetPeriodNumber(int n) => PeriodNumber = n;
}
