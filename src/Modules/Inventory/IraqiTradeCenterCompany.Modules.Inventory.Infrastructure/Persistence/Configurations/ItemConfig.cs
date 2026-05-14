using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence.Configurations;

public class ItemConfig : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.ToTable("Items");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Barcode).HasMaxLength(50);
        b.Property(x => x.NameAr).HasMaxLength(300).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.PurchasePrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.BaseSalesPrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.MediumUnitFactor).HasColumnType("decimal(18,3)");
        b.Property(x => x.MediumSalesPrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.LargeUnitFactor).HasColumnType("decimal(18,3)");
        b.Property(x => x.LargeSalesPrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.StockBaseQuantity).HasColumnType("decimal(18,3)");
        b.Property(x => x.MinimumStockLevel).HasColumnType("decimal(18,3)");
        b.Property(x => x.MaximumStockLevel).HasColumnType("decimal(18,3)");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Barcode);
        b.HasIndex(x => x.CategoryId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class WarehouseConfig : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.ToTable("Warehouses");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.Address).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class StockMovementConfig : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("StockMovements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        b.Property(x => x.ConversionFactor).HasColumnType("decimal(18,3)");
        b.Property(x => x.QuantityInBase).HasColumnType("decimal(18,3)");
        b.Property(x => x.QuantityBefore).HasColumnType("decimal(18,3)");
        b.Property(x => x.QuantityAfter).HasColumnType("decimal(18,3)");
        b.Property(x => x.UnitCost).HasColumnType("decimal(18,3)");
        b.Property(x => x.TotalValue).HasColumnType("decimal(18,3)");
        b.Property(x => x.ReferenceType).HasMaxLength(50);
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.ItemId);
        b.HasIndex(x => x.MovementDate);
        b.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
