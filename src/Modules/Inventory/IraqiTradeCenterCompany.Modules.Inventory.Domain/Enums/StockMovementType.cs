namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Enums;

public enum StockMovementType
{
    PurchaseIn = 1,
    SalesOut = 2,
    SalesReturn = 3,
    PurchaseReturn = 4,
    Adjustment = 5,
    Transfer = 6,
    Damaged = 7,
    OpeningBalance = 8
}
