namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;

public enum JournalEntryStatus { Draft = 1, Posted = 2, Reversed = 3 }

public enum JournalEntrySource
{
    Manual = 1,
    SalesInvoice = 2,
    PurchaseInvoice = 3,
    Payment = 4,
    Receipt = 5,
    StockMovement = 6,
    CommissionPayment = 7,
    SalaryPayment = 8,
    System = 9
}

public enum PeriodStatus { Open = 1, Closed = 2, Locked = 3 }
