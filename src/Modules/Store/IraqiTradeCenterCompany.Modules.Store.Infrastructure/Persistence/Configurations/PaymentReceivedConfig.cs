using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Configurations;

public class PaymentReceivedConfig : IEntityTypeConfiguration<PaymentReceived>
{
    public void Configure(EntityTypeBuilder<PaymentReceived> b)
    {
        b.ToTable("PaymentsReceived");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReceiptNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Amount).HasColumnType("decimal(18,3)");
        b.Property(x => x.PaymentMethod).HasMaxLength(50).IsRequired();
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.ReceiptNumber).IsUnique();
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.SalesInvoiceId);
        b.HasIndex(x => x.PaymentDate);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
