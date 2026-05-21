using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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
    DbSet<CurrencyRateBulletin> CurrencyRateBulletins { get; }
    DbSet<CurrencyRateLine> CurrencyRateLines { get; }
    DbSet<JournalVoucherType> JournalVoucherTypes { get; }
    DbSet<CashBox> CashBoxes { get; }
    DbSet<CashBoxCurrency> CashBoxCurrencies { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// الحصول على رقم القيد التسلسلي التالي ضمن السنة المالية المحددة.
    /// كل سنة مالية تبدأ من 1. يجب استدعاؤها داخل معاملة (BeginTransactionAsync)
    /// لضمان الذرّية وعدم تكرار الأرقام عند الطلبات المتزامنة.
    /// </summary>
    Task<long> GetNextJournalEntryNumberAsync(int fiscalYearId, CancellationToken ct = default);

    /// <summary>
    /// الحصول على التسلسل التالي لرقم السند ضمن نوع سند محدد (يبدأ من 1 لكل نوع).
    /// يُستعمل لتوليد أرقام مثل PV-1, PV-2, RV-1 … بحسب رمز نوع السند في الواجهة.
    /// يجب استدعاؤها داخل معاملة لضمان الذرّية ومنع تكرار التسلسل.
    /// </summary>
    Task<int> GetNextVoucherSequenceAsync(int voucherTypeId, CancellationToken ct = default);

    /// <summary>
    /// بدء معاملة قاعدة بيانات صريحة (للأمر يحتاج ذرية عبر عدّة عمليات).
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// الحصول على اتصال DB الخام (لاستعلامات SQL مخصصة).
    /// </summary>
    System.Data.Common.DbConnection GetDbConnection();
}
