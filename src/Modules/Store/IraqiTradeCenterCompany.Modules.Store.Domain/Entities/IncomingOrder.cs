using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

/// <summary>
/// طلبية واردة من المنصة الأم (من تاجر).
/// PlatformOrderId مرجع لـ Order في DB الأم.
/// </summary>
public class IncomingOrder : BaseEntity
{
    public Guid PlatformOrderId { get; private set; }
    public string PlatformOrderNumber { get; private set; } = default!;
    public DateTime ReceivedAt { get; private set; }
    public int CustomerId { get; private set; }
    public int? AssignedSalesRepId { get; private set; }
    public OrderProcessingStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? Notes { get; private set; }
    public int? CreatedInvoiceId { get; private set; }      // عند التأكيد
    public DateTime? ConfirmedAt { get; private set; }

    public virtual ICollection<IncomingOrderItem> Items { get; private set; } = new List<IncomingOrderItem>();

    private IncomingOrder() { }

    public static IncomingOrder Receive(Guid platformOrderId, string platformOrderNumber,
                                         int customerId, decimal totalAmount)
        => new()
        {
            PlatformOrderId = platformOrderId, PlatformOrderNumber = platformOrderNumber,
            ReceivedAt = DateTime.UtcNow, CustomerId = customerId,
            Status = OrderProcessingStatus.Pending, TotalAmount = totalAmount
        };

    public void AddItem(int itemId, string itemName, int unitId, decimal quantity, decimal unitPrice)
    {
        if (Status != OrderProcessingStatus.Pending) throw new DomainException("الطلبية ليست بحالة استلام");
        Items.Add(IncomingOrderItem.Create(itemId, itemName, unitId, quantity, unitPrice));
    }

    public void AssignSalesRep(int repId)
    {
        AssignedSalesRepId = repId;
        if (Status == OrderProcessingStatus.Pending) Status = OrderProcessingStatus.Reviewed;
    }

    public void Confirm(int invoiceId)
    {
        if (Status == OrderProcessingStatus.Confirmed) throw new DomainException("الطلبية مؤكدة مسبقاً");
        if (Status == OrderProcessingStatus.Rejected) throw new DomainException("الطلبية مرفوضة");
        Status = OrderProcessingStatus.Confirmed;
        CreatedInvoiceId = invoiceId;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Reject(string reason)
    {
        Status = OrderProcessingStatus.Rejected;
        Notes = (Notes ?? string.Empty) + $"\nرفض: {reason}";
    }
}
