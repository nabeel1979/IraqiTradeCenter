using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class PaymentReceived : BaseEntity
{
    public string ReceiptNumber { get; private set; } = default!;
    public DateTime PaymentDate { get; private set; }
    public int CustomerId { get; private set; }
    public int? SalesInvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethod { get; private set; } = default!;  // "Cash", "Bank", "ZainCash", ...
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public int CashAccountId { get; private set; }                  // أي حساب نقدي/بنكي استلم
    public int? JournalEntryId { get; private set; }

    private PaymentReceived() { }

    public static PaymentReceived Create(int customerId, int? invoiceId, decimal amount,
                                          string method, int cashAccountId, string? refNumber = null)
    {
        if (amount <= 0) throw new DomainException("المبلغ لازم موجب");
        return new PaymentReceived
        {
            ReceiptNumber = GenerateNumber(),
            PaymentDate = DateTime.UtcNow.Date,
            CustomerId = customerId, SalesInvoiceId = invoiceId,
            Amount = amount, PaymentMethod = method,
            CashAccountId = cashAccountId, ReferenceNumber = refNumber
        };
    }

    public void LinkJournalEntry(int journalEntryId) => JournalEntryId = journalEntryId;

    private static string GenerateNumber()
        => $"REC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
}
