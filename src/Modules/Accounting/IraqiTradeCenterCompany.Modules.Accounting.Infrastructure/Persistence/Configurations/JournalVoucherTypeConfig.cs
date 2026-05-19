using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Configurations;

public class JournalVoucherTypeConfig : IEntityTypeConfiguration<JournalVoucherType>
{
    public void Configure(EntityTypeBuilder<JournalVoucherType> b)
    {
        b.ToTable("JournalVoucherTypes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(150);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.IsEnabled).HasDefaultValue(true).IsRequired();
        b.Property(x => x.IsSystem).HasDefaultValue(false).IsRequired();
        b.Property(x => x.DisplayOrder).HasDefaultValue(100).IsRequired();
        b.Property(x => x.Nature).HasConversion<int>().HasDefaultValue(Domain.Enums.VoucherNature.Mixed).IsRequired();
        b.Property(x => x.ShowInSidebar).HasDefaultValue(false).IsRequired();

        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        b.HasIndex(x => x.DisplayOrder);

        // الحسابات الافتراضية: علاقة اختيارية بحساب من الدليل (NoAction كي لا تُحذف الحسابات بالخطأ).
        b.HasOne(x => x.DefaultDebitAccount)
            .WithMany()
            .HasForeignKey(x => x.DefaultDebitAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.DefaultCreditAccount)
            .WithMany()
            .HasForeignKey(x => x.DefaultCreditAccountId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
