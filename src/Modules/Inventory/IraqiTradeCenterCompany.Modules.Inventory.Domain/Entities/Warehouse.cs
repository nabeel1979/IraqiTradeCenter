using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;

public class Warehouse : BaseEntity
{
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? KeeperName { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private Warehouse() { }
    public static Warehouse Create(string code, string nameAr, bool isDefault = false)
        => new() { Code = code, NameAr = nameAr, IsDefault = isDefault, IsActive = true };
}
