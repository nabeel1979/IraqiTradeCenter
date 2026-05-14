using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence.Configurations;

public class UnitOfMeasureConfig : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> b)
    {
        b.ToTable("UnitsOfMeasure");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).HasMaxLength(50).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(50);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class ItemCategoryConfig : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> b)
    {
        b.ToTable("ItemCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
