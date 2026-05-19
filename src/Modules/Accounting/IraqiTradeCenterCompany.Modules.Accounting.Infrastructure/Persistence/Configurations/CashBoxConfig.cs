using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class CashBoxConfig : IEntityTypeConfiguration<CashBox>
{
    public void Configure(EntityTypeBuilder<CashBox> b)
    {
        b.ToTable("CashBoxes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        b.Property(x => x.DisplayOrder).HasDefaultValue(100).IsRequired();

        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.AccountId);
        b.HasIndex(x => x.DisplayOrder);

        b.HasOne(x => x.Account)
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.Currencies)
            .WithOne(c => c.CashBox)
            .HasForeignKey(c => c.CashBoxId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CashBoxCurrencyConfig : IEntityTypeConfiguration<CashBoxCurrency>
{
    public void Configure(EntityTypeBuilder<CashBoxCurrency> b)
    {
        b.ToTable("CashBoxCurrencies");
        b.HasKey(x => x.Id);

        b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        b.Property(x => x.DebitLimit).HasColumnType("decimal(18,3)");
        b.Property(x => x.CreditLimit).HasColumnType("decimal(18,3)");
        b.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();

        b.HasIndex(x => new { x.CashBoxId, x.Currency }).IsUnique().HasFilter("[IsDeleted] = 0");

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
