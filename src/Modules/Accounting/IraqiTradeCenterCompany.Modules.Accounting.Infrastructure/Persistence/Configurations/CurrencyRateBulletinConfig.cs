using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class CurrencyRateBulletinConfig : IEntityTypeConfiguration<CurrencyRateBulletin>
{
    public void Configure(EntityTypeBuilder<CurrencyRateBulletin> b)
    {
        b.ToTable("CurrencyRateBulletins");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.BaseCurrency).HasMaxLength(10).IsRequired();
        b.Property(x => x.EffectiveAt).IsRequired();
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.PublishedBy).HasMaxLength(100);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.EffectiveAt);
        b.HasIndex(x => x.Status);
        b.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.CurrencyRateBulletinId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CurrencyRateLineConfig : IEntityTypeConfiguration<CurrencyRateLine>
{
    public void Configure(EntityTypeBuilder<CurrencyRateLine> b)
    {
        b.ToTable("CurrencyRateLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        b.Property(x => x.Rate).HasColumnType("decimal(18,6)").IsRequired();
        b.Property(x => x.Operation).HasConversion<int>().IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => new { x.CurrencyRateBulletinId, x.Currency }).IsUnique();
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
