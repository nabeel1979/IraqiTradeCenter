using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class CommissionTransaction : BaseEntity
{
    public int SalesRepId { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public decimal TotalSales { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public bool IsPaid { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public int? JournalEntryId { get; private set; }

    private CommissionTransaction() { }

    public static CommissionTransaction Create(int repId, DateTime start, DateTime end, decimal totalSales, decimal commission)
        => new()
        {
            SalesRepId = repId, PeriodStart = start, PeriodEnd = end,
            TotalSales = totalSales, CommissionAmount = commission
        };

    public void MarkAsPaid(int journalEntryId)
    {
        IsPaid = true; PaidAt = DateTime.UtcNow; JournalEntryId = journalEntryId;
    }
}
