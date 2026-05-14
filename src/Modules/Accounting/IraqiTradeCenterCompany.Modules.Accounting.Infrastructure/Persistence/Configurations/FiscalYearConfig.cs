using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class FiscalYearConfig : IEntityTypeConfiguration<FiscalYear>
{
    public void Configure(EntityTypeBuilder<FiscalYear> b)
    {
        b.ToTable("FiscalYears");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasMany(x => x.Periods).WithOne().HasForeignKey(p => p.FiscalYearId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class AccountingPeriodConfig : IEntityTypeConfiguration<AccountingPeriod>
{
    public void Configure(EntityTypeBuilder<AccountingPeriod> b)
    {
        b.ToTable("AccountingPeriods");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasConversion<int>();
        b.HasIndex(x => new { x.FiscalYearId, x.PeriodNumber }).IsUnique();
        b.HasIndex(x => new { x.StartDate, x.EndDate });
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
