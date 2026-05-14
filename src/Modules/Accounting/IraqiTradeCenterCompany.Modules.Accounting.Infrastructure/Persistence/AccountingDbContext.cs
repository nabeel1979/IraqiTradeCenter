using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;

/// <summary>
/// DbContext خاص بمودول المحاسبة - يستخدم schema منفصل "acc"
/// </summary>
public class AccountingDbContext : DbContext, IAccountingDbContext
{
    public const string Schema = "acc";
    private readonly ICurrentUserService? _currentUser;

    public AccountingDbContext(DbContextOptions<AccountingDbContext> options,
                                ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var userId = _currentUser?.UserId?.ToString() ?? "system";
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.SetCreated(userId);
            else if (entry.State == EntityState.Modified) entry.Entity.SetUpdated(userId);
        }
        return base.SaveChangesAsync(ct);
    }
}
