using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class AccountConfig : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.ToTable("Accounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Nature).HasConversion<int>();
        b.Property(x => x.OpeningBalance).HasColumnType("decimal(18,3)");
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.ParentId);
        b.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
