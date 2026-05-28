using IraqiTradeCenterCompany.API.Auth.Auditing;
using IraqiTradeCenterCompany.API.Auth.Notifications;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Settings;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<CompanyUser> Users => Set<CompanyUser>();
    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
    public DbSet<Currency> Currencies => Set<Currency>();

    // ── Permissions module
    public DbSet<Permission>              Permissions             => Set<Permission>();
    public DbSet<Role>                    Roles                   => Set<Role>();
    public DbSet<RolePermission>          RolePermissions         => Set<RolePermission>();
    public DbSet<UserRole>                UserRoles               => Set<UserRole>();
    public DbSet<UserPermissionOverride>  UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<UserCashBox>             UserCashBoxes           => Set<UserCashBox>();

    // ── Audit / monitoring
    public DbSet<AuditLog>                AuditLogs               => Set<AuditLog>();

    // ── Notifications
    public DbSet<Notification>            Notifications           => Set<Notification>();

    // ── Attachment storage settings (singleton row)
    public DbSet<AttachmentStorageSettings> AttachmentStorageSettings => Set<AttachmentStorageSettings>();

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
            e.Property(x => x.Preferences).HasColumnType("nvarchar(max)");
        });

        // ── Permission (lookup, مفتاحه الكود نفسه)
        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permissions", "auth");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(100).IsRequired();
            e.Property(x => x.Module).HasMaxLength(50).IsRequired();
            e.Property(x => x.Resource).HasMaxLength(50).IsRequired();
            e.Property(x => x.Action).HasMaxLength(20).IsRequired();
            e.Property(x => x.NameAr).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => x.Module);
        });

        // ── Role
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles", "auth");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.NameAr).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
        });

        // ── RolePermission (M:N) — composite PK
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions", "auth");
            e.HasKey(x => new { x.RoleId, x.PermissionCode });
            e.Property(x => x.PermissionCode).HasMaxLength(100);
            e.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserRole (M:N) — composite PK
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles", "auth");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserPermissionOverride — composite PK
        modelBuilder.Entity<UserPermissionOverride>(e =>
        {
            e.ToTable("UserPermissionOverrides", "auth");
            e.HasKey(x => new { x.UserId, x.PermissionCode });
            e.Property(x => x.PermissionCode).HasMaxLength(100);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserCashBox — composite PK (CashBox FK معالج كرابط منطقي فقط لأنه بـ schema آخر)
        modelBuilder.Entity<UserCashBox>(e =>
        {
            e.ToTable("UserCashBoxes", "auth");
            e.HasKey(x => new { x.UserId, x.CashBoxId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CashBoxId);
        });

        // ── AuditLog (append-only)
        //   لا FKs خارج auth schema حتى لا تُحظَر الكتابة عند soft-delete لكيانات
        //   تابعة لـ schema آخر. الفهارس مُصمَّمة لـ (الكيان+الـ Id) و(الوقت).
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(400);
            e.Property(x => x.DetailsJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.UserName).HasMaxLength(150);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.OccurredAtUtc);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.Action);
        });

        modelBuilder.Entity<CompanySettings>(e =>
        {
            e.ToTable("CompanySettings", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.NameAr).HasMaxLength(200).IsRequired();
            e.Property(x => x.NameEn).HasMaxLength(200);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.AddressEn).HasMaxLength(500);
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

        // ── Notifications
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(500).IsRequired();
            e.Property(x => x.Link).HasMaxLength(300);
            e.Property(x => x.EntityType).HasMaxLength(50);
            e.Property(x => x.EntityId).HasMaxLength(50);
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasIndex(x => x.CreatedAt);
        });

        // ── AttachmentStorageSettings (singleton: Id=1)
        modelBuilder.Entity<AttachmentStorageSettings>(e =>
        {
            e.ToTable("AttachmentStorageSettings", "auth");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Provider).HasMaxLength(20).IsRequired();
            e.Property(x => x.LocalRootPath).HasMaxLength(500);
            e.Property(x => x.R2AccountId).HasMaxLength(100);
            e.Property(x => x.R2AccessKeyId).HasMaxLength(200);
            e.Property(x => x.R2SecretAccessKey).HasMaxLength(500);
            e.Property(x => x.R2Bucket).HasMaxLength(100);
            e.Property(x => x.R2PublicBaseUrl).HasMaxLength(300);
            e.Property(x => x.UpdatedBy).HasMaxLength(100);
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
