using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;

public class UnbalancedJournalEntryException : DomainException
{
    public UnbalancedJournalEntryException(decimal debit, decimal credit)
        : base($"القيد غير متوازن! المدين: {debit:N3} | الدائن: {credit:N3} | الفرق: {Math.Abs(debit - credit):N3}") { }
}

public class ClosedPeriodException : DomainException
{
    public ClosedPeriodException(DateTime date)
        : base($"الفترة المحاسبية ليوم {date:yyyy-MM-dd} مغلقة - لا يمكن إضافة قيود فيها") { }
}
