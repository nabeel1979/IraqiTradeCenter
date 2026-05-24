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
    public DbSet<CurrencyRateBulletin> CurrencyRateBulletins => Set<CurrencyRateBulletin>();
    public DbSet<CurrencyRateLine> CurrencyRateLines => Set<CurrencyRateLine>();
    public DbSet<JournalVoucherType> JournalVoucherTypes => Set<JournalVoucherType>();
    public DbSet<CashBox> CashBoxes => Set<CashBox>();
    public DbSet<CashBoxCurrency> CashBoxCurrencies => Set<CashBoxCurrency>();
    public DbSet<CashBoxTransfer> CashBoxTransfers => Set<CashBoxTransfer>();

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

    public async Task<long> GetNextJournalEntryNumberAsync(int fiscalYearId, CancellationToken ct = default)
    {
        // ترقيم متسلسل لكل سنة مالية:
        //  - نقفل ذرّياً على مستوى السنة المالية بـ sp_getapplock لمنع تكرار الرقم
        //    عند الطلبات المتزامنة (UPDLOCK وحده لا يحجز شيئاً عند عدم وجود صفوف).
        //  - نأخذ MAX من كل القيود (سواء محذوفة أم لا) كي لا نُعيد استخدام أرقام
        //    قيود محذوفة سابقاً — مهم لمسار التدقيق المحاسبي.
        //  - يجب أن تكون هناك معاملة قائمة من الـ Caller لأن LockOwner='Transaction'
        //    يضمن استمرار القفل حتى تُلتزم المعاملة (بعد إدراج القيد).
        if (Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "GetNextJournalEntryNumberAsync يجب أن تُستدعى داخل معاملة (BeginTransactionAsync).");
        }

        // (1) نضع قفلاً ذرّياً على الـ resource المرتبط بالسنة المالية.
        //     LockOwner='Transaction' يضمن أن القفل يبقى رفيعاً حتى Commit/Rollback.
        var lockSql = @"
DECLARE @res INT;
EXEC @res = sp_getapplock
    @Resource    = @resource,
    @LockMode    = 'Exclusive',
    @LockOwner   = 'Transaction',
    @LockTimeout = 8000;
IF @res < 0
    THROW 51000, N'تعذّر الحصول على قفل توليد رقم القيد، حاول مرة أخرى', 1;";
        var resourceParam = new Microsoft.Data.SqlClient.SqlParameter("@resource",
            $"acc.JournalEntryNumber.FY:{fiscalYearId}");
        await Database.ExecuteSqlRawAsync(lockSql, new[] { resourceParam }, ct);

        // (2) نقرأ MAX من كل القيود (نشطة + محذوفة) ضمن نفس السنة، مع +1.
        //     نأخذ المحذوفة في الحسبان لمنع إعادة استخدام أرقام لقيود سابقة (audit trail).
        const string maxSql = @"
SELECT ISNULL(MAX(TRY_CAST(EntryNumber AS BIGINT)), 0) + 1 AS Value
FROM acc.JournalEntries
WHERE FiscalYearId = {0}
  AND TRY_CAST(EntryNumber AS BIGINT) IS NOT NULL";

        return await Database
            .SqlQueryRaw<long>(maxSql, fiscalYearId)
            .FirstAsync(ct);
    }

    public async Task<int> GetNextVoucherSequenceAsync(int voucherTypeId, CancellationToken ct = default)
    {
        // ترقيم مستقل لكل نوع سند: PV-1, PV-2 … RV-1 … (يبدأ من 1 لكل نوع).
        // نفس نمط GetNextJournalEntryNumberAsync لكن المورد مفصول حسب VoucherTypeId.
        if (Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "GetNextVoucherSequenceAsync يجب أن تُستدعى داخل معاملة (BeginTransactionAsync).");
        }

        var lockSql = @"
DECLARE @res INT;
EXEC @res = sp_getapplock
    @Resource    = @resource,
    @LockMode    = 'Exclusive',
    @LockOwner   = 'Transaction',
    @LockTimeout = 8000;
IF @res < 0
    THROW 51000, N'تعذّر الحصول على قفل توليد رقم السند، حاول مرة أخرى', 1;";
        var resourceParam = new Microsoft.Data.SqlClient.SqlParameter("@resource",
            $"acc.VoucherSequence.VT:{voucherTypeId}");
        await Database.ExecuteSqlRawAsync(lockSql, new[] { resourceParam }, ct);

        // نقرأ MAX من كل القيود (نشطة + محذوفة) لنفس نوع السند، مع +1.
        // إدراج المحذوفة يحفظ تسلسل التدقيق ويمنع إعادة استخدام نفس رقم سند.
        const string maxSql = @"
SELECT ISNULL(MAX(VoucherSequence), 0) + 1 AS Value
FROM acc.JournalEntries
WHERE VoucherTypeId = {0}
  AND VoucherSequence IS NOT NULL";

        return await Database
            .SqlQueryRaw<int>(maxSql, voucherTypeId)
            .FirstAsync(ct);
    }

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken ct = default)
        => Database.BeginTransactionAsync(ct);

    public System.Data.Common.DbConnection GetDbConnection() => Database.GetDbConnection();
}
