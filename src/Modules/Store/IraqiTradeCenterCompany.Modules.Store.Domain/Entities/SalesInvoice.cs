using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class SalesInvoice : BaseEntity
{
    public string InvoiceNumber { get; private set; } = default!;
    public DateTime InvoiceDate { get; private set; }
    public int CustomerId { get; private set; }
    public int? SalesRepId { get; private set; }
    public int? IncomingOrderId { get; private set; }      // مرجع للطلبية إن وجدت
    public InvoiceStatus Status { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal DiscountPercentage { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal TaxRate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public string? Notes { get; private set; }
    public DateTime? IssuedAt { get; private set; }
    public int? JournalEntryId { get; private set; }      // مرجع للقيد في acc.JournalEntries

    public virtual ICollection<SalesInvoiceLine> Lines { get; private set; } = new List<SalesInvoiceLine>();

    private SalesInvoice() { }

    public static SalesInvoice Create(int customerId, int? salesRepId, decimal taxRate, int? orderId = null)
    {
        return new SalesInvoice
        {
            InvoiceNumber = GenerateNumber(),
            InvoiceDate = DateTime.UtcNow.Date,
            CustomerId = customerId, SalesRepId = salesRepId,
            IncomingOrderId = orderId,
            Status = InvoiceStatus.Draft,
            TaxRate = taxRate
        };
    }

    public void AddLine(int itemId, string itemName, int unitId, string unitName,
                         decimal quantity, decimal conversionFactor, decimal unitPrice, decimal discount = 0)
    {
        if (Status != InvoiceStatus.Draft) throw new DomainException("لا يمكن تعديل فاتورة مرحلة");
        var line = SalesInvoiceLine.Create(itemId, itemName, unitId, unitName,
            quantity, conversionFactor, unitPrice, discount);
        Lines.Add(line);
        Recalculate();
    }

    public void ApplyDiscount(decimal percentage, decimal amount)
    {
        if (Status != InvoiceStatus.Draft) throw new DomainException("لا يمكن تعديل فاتورة مرحلة");
        DiscountPercentage = percentage;
        DiscountAmount = amount;
        Recalculate();
    }

    private void Recalculate()
    {
        SubTotal = Lines.Sum(l => l.LineTotal);
        if (DiscountPercentage > 0) DiscountAmount = Math.Round(SubTotal * DiscountPercentage / 100m, 3);
        var afterDiscount = SubTotal - DiscountAmount;
        TaxAmount = Math.Round(afterDiscount * TaxRate / 100m, 3);
        TotalAmount = afterDiscount + TaxAmount;
    }

    public void Issue()
    {
        if (Status != InvoiceStatus.Draft) throw new DomainException("الفاتورة ليست بحالة مسودة");
        if (!Lines.Any()) throw new DomainException("الفاتورة بدون أسطر");
        Status = InvoiceStatus.Issued;
        IssuedAt = DateTime.UtcNow;
    }

    public void LinkJournalEntry(int journalEntryId) => JournalEntryId = journalEntryId;

    public void RecordPayment(decimal amount)
    {
        if (Status == InvoiceStatus.Cancelled) throw new DomainException("الفاتورة ملغاة");
        if (amount <= 0) throw new DomainException("مبلغ الدفع لازم موجب");
        if (PaidAmount + amount > TotalAmount + 0.01m)
            throw new DomainException("مبلغ الدفع أكبر من المتبقي");
        PaidAmount += amount;
        Status = PaidAmount >= TotalAmount ? InvoiceStatus.Paid
            : (PaidAmount > 0 ? InvoiceStatus.PartiallyPaid : Status);
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Paid) throw new DomainException("لا يمكن إلغاء فاتورة مدفوعة");
        Status = InvoiceStatus.Cancelled;
    }

    private static string GenerateNumber()
        => $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
}
