using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class JournalEntry : BaseEntity
{
    public string EntryNumber { get; private set; } = default!;
    public DateTime EntryDate { get; private set; }
    public int FiscalYearId { get; private set; }
    public int AccountingPeriodId { get; private set; }
    public JournalEntryStatus Status { get; private set; }
    public JournalEntrySource Source { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public string? ReferenceType { get; private set; }
    public int? ReferenceId { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public DateTime? PostedAt { get; private set; }
    public string? PostedBy { get; private set; }
    public int? ReversedByEntryId { get; private set; }
    public virtual ICollection<JournalEntryLine> Lines { get; private set; } = new List<JournalEntryLine>();

    private JournalEntry() { }

    public static JournalEntry Create(DateTime date, int fyId, int periodId, JournalEntrySource source,
                                       string description, string? refType = null, int? refId = null, string? refNumber = null)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new DomainException("بيان القيد مطلوب");
        return new JournalEntry
        {
            EntryNumber = GenerateNumber(),
            EntryDate = date, FiscalYearId = fyId, AccountingPeriodId = periodId,
            Status = JournalEntryStatus.Draft, Source = source,
            Description = description,
            ReferenceType = refType, ReferenceId = refId, ReferenceNumber = refNumber
        };
    }

    public void AddDebit(int accountId, decimal amount, string? desc = null)
    {
        EnsureDraft();
        Lines.Add(JournalEntryLine.CreateDebit(accountId, amount, desc));
        Recalc();
    }

    public void AddCredit(int accountId, decimal amount, string? desc = null)
    {
        EnsureDraft();
        Lines.Add(JournalEntryLine.CreateCredit(accountId, amount, desc));
        Recalc();
    }

    public void Post(string postedBy)
    {
        if (Status != JournalEntryStatus.Draft) throw new DomainException("القيد ليس بحالة مسودة");
        if (Lines.Count < 2) throw new DomainException("القيد لازم سطرين على الأقل");
        Recalc();
        if (Math.Round(TotalDebit, 3) != Math.Round(TotalCredit, 3))
            throw new UnbalancedJournalEntryException(TotalDebit, TotalCredit);
        Status = JournalEntryStatus.Posted;
        PostedAt = DateTime.UtcNow;
        PostedBy = postedBy;
    }

    public JournalEntry CreateReversal(string reason)
    {
        if (Status != JournalEntryStatus.Posted) throw new DomainException("لا يمكن عكس قيد غير مرحّل");
        var rev = new JournalEntry
        {
            EntryNumber = GenerateNumber(),
            EntryDate = DateTime.UtcNow.Date,
            FiscalYearId = FiscalYearId, AccountingPeriodId = AccountingPeriodId,
            Status = JournalEntryStatus.Draft, Source = JournalEntrySource.Manual,
            Description = $"عكس قيد رقم {EntryNumber} - {reason}",
            ReferenceType = "ReversalOf", ReferenceId = Id
        };
        foreach (var l in Lines)
        {
            if (l.IsDebit) rev.AddCredit(l.AccountId, l.Amount, $"عكس: {l.Description}");
            else rev.AddDebit(l.AccountId, l.Amount, $"عكس: {l.Description}");
        }
        return rev;
    }

    public void MarkAsReversed(int revId) { Status = JournalEntryStatus.Reversed; ReversedByEntryId = revId; }

    private void EnsureDraft() { if (Status != JournalEntryStatus.Draft) throw new DomainException("لا يمكن تعديل قيد مرحّل"); }
    private void Recalc()
    {
        TotalDebit = Lines.Where(l => l.IsDebit).Sum(l => l.Amount);
        TotalCredit = Lines.Where(l => !l.IsDebit).Sum(l => l.Amount);
    }

    private static string GenerateNumber()
        => $"JE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
}
