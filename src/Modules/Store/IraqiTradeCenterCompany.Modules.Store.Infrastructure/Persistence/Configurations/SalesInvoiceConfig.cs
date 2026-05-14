using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Configurations;

public class SalesInvoiceConfig : IEntityTypeConfiguration<SalesInvoice>
{
    public void Configure(EntityTypeBuilder<SalesInvoice> b)
    {
        b.ToTable("SalesInvoices");
        b.HasKey(x => x.Id);
        b.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.SubTotal).HasColumnType("decimal(18,3)");
        b.Property(x => x.DiscountAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.DiscountPercentage).HasColumnType("decimal(5,2)");
        b.Property(x => x.TaxAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.TaxRate).HasColumnType("decimal(5,2)");
        b.Property(x => x.TotalAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.PaidAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Ignore(x => x.RemainingAmount);
        b.HasIndex(x => x.InvoiceNumber).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.SalesRepId);
        b.HasIndex(x => x.InvoiceDate);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.JournalEntryId);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class SalesInvoiceLineConfig : IEntityTypeConfiguration<SalesInvoiceLine>
{
    public void Configure(EntityTypeBuilder<SalesInvoiceLine> b)
    {
        b.ToTable("SalesInvoiceLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.ItemName).HasMaxLength(300).IsRequired();
        b.Property(x => x.UnitName).HasMaxLength(50).IsRequired();
        b.Property(x => x.Quantity).HasColumnType("decimal(18,3)");
        b.Property(x => x.ConversionFactor).HasColumnType("decimal(18,3)");
        b.Property(x => x.QuantityInBase).HasColumnType("decimal(18,3)");
        b.Property(x => x.UnitPrice).HasColumnType("decimal(18,3)");
        b.Property(x => x.LineDiscount).HasColumnType("decimal(18,3)");
        b.Property(x => x.LineTotal).HasColumnType("decimal(18,3)");
        b.HasIndex(x => x.SalesInvoiceId);
        b.HasIndex(x => x.ItemId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
