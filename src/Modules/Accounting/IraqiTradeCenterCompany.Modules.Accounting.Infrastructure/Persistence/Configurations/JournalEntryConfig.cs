using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class JournalEntryConfig : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> b)
    {
        b.ToTable("JournalEntries");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntryNumber).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Source).HasConversion<int>();
        b.Property(x => x.TotalDebit).HasColumnType("decimal(18,3)");
        b.Property(x => x.TotalCredit).HasColumnType("decimal(18,3)");
        b.Property(x => x.ReferenceType).HasMaxLength(50);
        b.Property(x => x.ReferenceNumber).HasMaxLength(100);
        b.Property(x => x.PostedBy).HasMaxLength(100);
        b.HasIndex(x => x.EntryNumber).IsUnique();
        b.HasIndex(x => x.EntryDate);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}

public class JournalEntryLineConfig : IEntityTypeConfiguration<JournalEntryLine>
{
    public void Configure(EntityTypeBuilder<JournalEntryLine> b)
    {
        b.ToTable("JournalEntryLines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasColumnType("decimal(18,3)");
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => x.AccountId);
        b.HasIndex(x => x.JournalEntryId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
