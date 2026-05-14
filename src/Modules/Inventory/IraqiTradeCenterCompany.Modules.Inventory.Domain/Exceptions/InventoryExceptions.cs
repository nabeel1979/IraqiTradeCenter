using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Exceptions;

public class InsufficientStockException : DomainException
{
    public InsufficientStockException(string item, decimal requested, decimal available)
        : base($"المخزون غير كافٍ للمادة '{item}'. المطلوب: {requested:N3} | المتاح: {available:N3}") { }
}
