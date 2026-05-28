using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class VoucherAttachmentConfig : IEntityTypeConfiguration<VoucherAttachment>
{
    public void Configure(EntityTypeBuilder<VoucherAttachment> b)
    {
        b.ToTable("VoucherAttachments");
        b.HasKey(x => x.Id);

        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        b.Property(x => x.StorageProvider).HasMaxLength(20).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(150);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.UploadedByUserName).HasMaxLength(150);
        b.Property(x => x.Notes).HasMaxLength(500);

        // ‎ربط هيدر القيد بمرفقاته (Cascade حذف ناعم — لا نتركها معلَّقة).
        b.HasOne(x => x.JournalEntry)
            .WithMany()
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // ‎فهرس قراءة سريع: كل مرفقات قيد محدّد بترتيب وقت الرفع.
        b.HasIndex(x => new { x.JournalEntryId, x.UploadedAtUtc });
        b.HasIndex(x => x.UploadedByUserId);
        // ‎فلتر الـ soft-delete على مستوى الكويري نتعامل معه عبر BaseEntity.IsDeleted.
        b.HasIndex(x => x.IsDeleted);
    }
}
