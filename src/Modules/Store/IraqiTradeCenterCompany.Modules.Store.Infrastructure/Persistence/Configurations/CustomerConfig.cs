using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Configurations;

public class CustomerConfig : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.BusinessName).HasMaxLength(200).IsRequired();
        b.Property(x => x.OwnerName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(15).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.CreditLimit).HasColumnType("decimal(18,3)");
        b.Property(x => x.CurrentBalance).HasColumnType("decimal(18,3)");
        b.HasIndex(x => x.PlatformUserId);
        b.HasIndex(x => x.PlatformTraderId);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Phone);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
