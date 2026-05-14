using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class SalesInvoiceLine : BaseEntity
{
    public int SalesInvoiceId { get; private set; }
    public int ItemId { get; private set; }
    public string ItemName { get; private set; } = default!;     // snapshot
    public int UnitOfMeasureId { get; private set; }
    public string UnitName { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal ConversionFactor { get; private set; }        // كم في وحدة الأساس
    public decimal QuantityInBase { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineDiscount { get; private set; }
    public decimal LineTotal { get; private set; }

    private SalesInvoiceLine() { }

    internal static SalesInvoiceLine Create(int itemId, string itemName, int unitId, string unitName,
        decimal quantity, decimal factor, decimal price, decimal lineDiscount)
    {
        if (quantity <= 0) throw new DomainException("الكمية لازم موجبة");
        if (price < 0) throw new DomainException("السعر سالب");

        var l = new SalesInvoiceLine
        {
            ItemId = itemId, ItemName = itemName,
            UnitOfMeasureId = unitId, UnitName = unitName,
            Quantity = quantity, ConversionFactor = factor,
            QuantityInBase = quantity * factor,
            UnitPrice = price, LineDiscount = lineDiscount
        };
        l.LineTotal = Math.Round((quantity * price) - lineDiscount, 3);
        return l;
    }
}
