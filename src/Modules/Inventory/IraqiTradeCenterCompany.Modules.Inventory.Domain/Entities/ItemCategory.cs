using IraqiTradeCenterCompany.SharedKernel.Common;

namespace IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;

public class ItemCategory : BaseEntity
{
    public string NameAr { get; private set; } = default!;
    public int? ParentId { get; private set; }
    public int Level { get; private set; }
    public virtual ItemCategory? Parent { get; private set; }
    public virtual ICollection<ItemCategory> Children { get; private set; } = new List<ItemCategory>();

    private ItemCategory() { }
    public static ItemCategory Create(string nameAr, int? parentId = null, int level = 1)
        => new() { NameAr = nameAr, ParentId = parentId, Level = level };
}
