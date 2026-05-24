using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class CashBoxTransferConfig : IEntityTypeConfiguration<CashBoxTransfer>
{
    public void Configure(EntityTypeBuilder<CashBoxTransfer> b)
    {
        b.ToTable("CashBoxTransfers");
        b.HasKey(x => x.Id);

        b.Property(x => x.TransferNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
        b.Property(x => x.Amount).HasColumnType("decimal(18,3)").IsRequired();
        b.Property(x => x.SendDate).IsRequired();
        b.Property(x => x.ReceiveDate).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.ReferenceNumber).HasMaxLength(50);

        // ‎الحالة + موافقة الاستلام + الإلغاء
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.ReceivedByUserId).HasMaxLength(100);
        b.Property(x => x.ReceiveNotes).HasMaxLength(500);
        b.Property(x => x.CancelledByUserId).HasMaxLength(100);
        b.Property(x => x.CancellationReason).HasMaxLength(500);

        b.HasIndex(x => x.TransferNumber).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.FromCashBoxId);
        b.HasIndex(x => x.ToCashBoxId);
        b.HasIndex(x => x.SendDate);
        b.HasIndex(x => x.ReceiveDate);
        b.HasIndex(x => x.SendJournalEntryId);
        b.HasIndex(x => x.ReceiveJournalEntryId);
        b.HasIndex(x => x.Status);

        b.HasOne(x => x.FromCashBox)
            .WithMany()
            .HasForeignKey(x => x.FromCashBoxId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.ToCashBox)
            .WithMany()
            .HasForeignKey(x => x.ToCashBoxId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.TransitAccount)
            .WithMany()
            .HasForeignKey(x => x.TransitAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.SendJournalEntry)
            .WithMany()
            .HasForeignKey(x => x.SendJournalEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.ReceiveJournalEntry)
            .WithMany()
            .HasForeignKey(x => x.ReceiveJournalEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.ReversalJournalEntry)
            .WithMany()
            .HasForeignKey(x => x.ReversalJournalEntryId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
