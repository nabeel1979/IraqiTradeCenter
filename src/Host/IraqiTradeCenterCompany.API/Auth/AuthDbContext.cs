using IraqiTradeCenterCompany.API.Settings;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<CompanyUser> Users => Set<CompanyUser>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("auth");
        modelBuilder.Entity<CompanyUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Phone).IsUnique();
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<CompanySettings>(e =>
        {
            e.ToTable("CompanySettings", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            e.Property(x => x.NameEn).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Website).HasMaxLength(200);
            e.Property(x => x.TaxNumber).HasMaxLength(50);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.ExchangeRatesJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.PrintHeader).HasMaxLength(500);
            e.Property(x => x.PrintFooter).HasMaxLength(500);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            // اللوكو يخزن كـ data URI (base64) - حد أقصى ~5MB
            e.Property(x => x.LogoBase64).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<Currency>(e =>
        {
            e.ToTable("Currencies", "auth");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10).IsRequired();
            e.Property(x => x.NumericCode).HasMaxLength(3);
            e.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            e.Property(x => x.NameEn).HasMaxLength(100);
            e.Property(x => x.Symbol).HasMaxLength(10);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
            e.HasIndex(x => x.IsBase);
            e.HasIndex(x => x.IsEnabled);
        });
    }
}
