using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class IncomingOrderItem : BaseEntity
{
    public int IncomingOrderId { get; private set; }
    public int ItemId { get; private set; }
    public string ItemName { get; private set; } = default!;
    public int UnitOfMeasureId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    private IncomingOrderItem() { }

    internal static IncomingOrderItem Create(int itemId, string itemName, int unitId, decimal quantity, decimal price)
        => new()
        {
            ItemId = itemId, ItemName = itemName, UnitOfMeasureId = unitId,
            Quantity = quantity, UnitPrice = price,
            LineTotal = Math.Round(quantity * price, 3)
        };
}
