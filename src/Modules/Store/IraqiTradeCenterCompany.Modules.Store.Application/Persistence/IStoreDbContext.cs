using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Persistence;

public interface IStoreDbContext
{
    DbSet<Customer> Customers { get; }
    DbSet<SalesRep> SalesReps { get; }
    DbSet<CommissionTier> CommissionTiers { get; }
    DbSet<CommissionTransaction> CommissionTransactions { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceLine> SalesInvoiceLines { get; }
    DbSet<PaymentReceived> PaymentsReceived { get; }
    DbSet<IncomingOrder> IncomingOrders { get; }
    DbSet<IncomingOrderItem> IncomingOrderItems { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
