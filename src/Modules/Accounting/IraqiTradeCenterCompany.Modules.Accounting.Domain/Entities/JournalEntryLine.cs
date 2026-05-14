using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class JournalEntryLine : BaseEntity
{
    public int JournalEntryId { get; private set; }
    public int AccountId { get; private set; }
    public bool IsDebit { get; private set; }
    public decimal Amount { get; private set; }
    public string? Description { get; private set; }

    private JournalEntryLine() { }

    internal static JournalEntryLine CreateDebit(int accId, decimal amt, string? desc)
    {
        if (amt <= 0) throw new DomainException("المبلغ لازم موجب");
        return new JournalEntryLine { AccountId = accId, IsDebit = true, Amount = amt, Description = desc };
    }

    internal static JournalEntryLine CreateCredit(int accId, decimal amt, string? desc)
    {
        if (amt <= 0) throw new DomainException("المبلغ لازم موجب");
        return new JournalEntryLine { AccountId = accId, IsDebit = false, Amount = amt, Description = desc };
    }
}
