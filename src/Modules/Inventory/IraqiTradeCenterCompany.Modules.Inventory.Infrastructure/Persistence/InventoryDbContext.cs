using IraqiTradeCenterCompany.Modules.Inventory.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Inventory.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Inventory.Infrastructure.Persistence;

public class InventoryDbContext : DbContext, IInventoryDbContext
{
    public const string Schema = "inv";
    private readonly ICurrentUserService? _currentUser;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options,
                               ICurrentUserService? currentUser = null) : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
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
