using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;

/// <summary>
/// DbContext خاص بمودول المحاسبة فقط - لا يُكشف للمودولز الأخرى.
/// </summary>
public interface IAccountingDbContext
{
    DbSet<FiscalYear> FiscalYears { get; }
    DbSet<AccountingPeriod> AccountingPeriods { get; }
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
