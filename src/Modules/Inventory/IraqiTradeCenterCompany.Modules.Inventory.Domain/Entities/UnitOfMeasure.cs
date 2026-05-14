using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;

public class UnitOfMeasure : BaseEntity
{
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public string Code { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private UnitOfMeasure() { }
    public static UnitOfMeasure Create(string nameAr, string code, string? nameEn = null)
        => new() { NameAr = nameAr, NameEn = nameEn, Code = code, IsActive = true };
}
