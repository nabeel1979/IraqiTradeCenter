using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Configurations;

public class IncomingOrderConfig : IEntityTypeConfiguration<IncomingOrder>
{
    public void Configure(EntityTypeBuilder<IncomingOrder> b)
    {
        b.ToTable("IncomingOrders");
        b.HasKey(x => x.Id);
        b.Property(x => x.PlatformOrderNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.PlatformOrderId).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.Status);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.IncomingOrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class IncomingOrderItemConfig : IEntityTypeConfiguration<IncomingOrderItem>
{
    public void Configure(EntityTypeBuilder<IncomingOrderItem> b)
    {
        b.ToTable("IncomingOrderItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemName).HasMaxLength(300).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        b.Property(x => x.UnitPrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.LineTotal).HasColumnType("decimal(18,3)");
        b.HasIndex(x => x.IncomingOrderId);
        b.HasIndex(x => x.ItemId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
