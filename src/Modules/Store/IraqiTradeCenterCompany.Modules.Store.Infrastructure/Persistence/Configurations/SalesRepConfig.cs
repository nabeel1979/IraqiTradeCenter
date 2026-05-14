using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence.Configurations;

public class SalesRepConfig : IEntityTypeConfiguration<SalesRep>
{
    public void Configure(EntityTypeBuilder<SalesRep> b)
    {
        b.ToTable("SalesReps");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmployeeCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(15).IsRequired();
        b.Property(x => x.Region).HasMaxLength(100);
        b.Property(x => x.BaseSalary).HasColumnType("decimal(18,3)");
        b.Property(x => x.FixedCommissionRate).HasColumnType("decimal(5,2)");
        b.Property(x => x.CommissionType).HasConversion<int>();
        b.HasIndex(x => x.EmployeeCode).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasMany(x => x.Tiers).WithOne().HasForeignKey(t => t.SalesRepId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CommissionTierConfig : IEntityTypeConfiguration<CommissionTier>
{
    public void Configure(EntityTypeBuilder<CommissionTier> b)
    {
        b.ToTable("CommissionTiers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FromSalesAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.ToSalesAmount).HasColumnType("decimal(18,3)");
        b.Property(x => x.CommissionRate).HasColumnType("decimal(5,2)");
        b.HasIndex(x => x.SalesRepId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class CommissionTransactionConfig : IEntityTypeConfiguration<CommissionTransaction>
{
    public void Configure(EntityTypeBuilder<CommissionTransaction> b)
    {
        b.ToTable("CommissionTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.TotalSales).HasColumnType("decimal(18,3)");
        b.Property(x => x.CommissionAmount).HasColumnType("decimal(18,3)");
        b.HasIndex(x => x.SalesRepId);
        b.HasIndex(x => new { x.PeriodStart, x.PeriodEnd });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
