using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Infrastructure.Persistence;

public class StoreDbContext : DbContext, IStoreDbContext
{
    public const string Schema = "store";
    private readonly ICurrentUserService? _currentUser;

    public StoreDbContext(DbContextOptions<StoreDbContext> options,
                          ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SalesRep> SalesReps => Set<SalesRep>();
    public DbSet<CommissionTier> CommissionTiers => Set<CommissionTier>();
    public DbSet<CommissionTransaction> CommissionTransactions => Set<CommissionTransaction>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceLine> SalesInvoiceLines => Set<SalesInvoiceLine>();
    public DbSet<PaymentReceived> PaymentsReceived => Set<PaymentReceived>();
    public DbSet<IncomingOrder> IncomingOrders => Set<IncomingOrder>();
    public DbSet<IncomingOrderItem> IncomingOrderItems => Set<IncomingOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreDbContext).Assembly);
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
