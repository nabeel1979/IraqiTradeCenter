using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Services;

/// <summary>
/// التطبيق الفعلي للـ IAccountingService - يستخدمه Store + Inventory عبر الـ Contract.
/// </summary>
public class AccountingService : IAccountingService
{
    private readonly AccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public AccountingService(AccountingDbContext db, IPeriodResolver periods, ICurrentUserService currentUser)
    {
        _db = db; _periods = periods; _currentUser = currentUser;
    }

    public async Task<int> CreateAutomaticJournalEntryAsync(CreateAutomaticEntryRequest request, CancellationToken ct = default)
    {
        var (fyId, periodId) = await _periods.ResolveAsync(request.EntryDate, ct);

        // ترجمة الأكواد إلى Account IDs
        var codes = request.Lines.Select(l => l.AccountCode).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => codes.Contains(a.Code) && a.IsLeaf && a.IsActive)
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);

        var missing = codes.Where(c => !accounts.ContainsKey(c)).ToList();
        if (missing.Any())
            throw new DomainException($"الحسابات التالية غير موجودة في شجرة الحسابات: {string.Join(", ", missing)}");

        var source = ParseSource(request.SourceCode);
        var entry = JournalEntry.Create(request.EntryDate, fyId, periodId, source,
            request.Description, request.ReferenceType, request.ReferenceId, request.ReferenceNumber);

        foreach (var l in request.Lines)
        {
            var accId = accounts[l.AccountCode];
            if (l.IsDebit) entry.AddDebit(accId, l.Amount, l.Description);
            else entry.AddCredit(accId, l.Amount, l.Description);
        }

        entry.Post(_currentUser.UserId?.ToString() ?? "system");
        await _db.JournalEntries.AddAsync(entry, ct);
        await _db.SaveChangesAsync(ct);
        return entry.Id;
    }

    public async Task<int> GetAccountIdByCodeAsync(string code, CancellationToken ct = default)
    {
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == code && a.IsLeaf, ct)
            ?? throw new DomainException($"حساب '{code}' غير موجود");
        return account.Id;
    }

    public async Task EnsurePeriodOpenAsync(DateTime date, CancellationToken ct = default)
    {
        await _periods.ResolveAsync(date, ct);  // يرمي خطأ إذا مغلقة
    }

    private static JournalEntrySource ParseSource(string code) => code switch
    {
        "SalesInvoice" => JournalEntrySource.SalesInvoice,
        "PurchaseInvoice" => JournalEntrySource.PurchaseInvoice,
        "Payment" => JournalEntrySource.Payment,
        "Receipt" => JournalEntrySource.Receipt,
        "StockMovement" => JournalEntrySource.StockMovement,
        "Commission" or "CommissionPayment" => JournalEntrySource.CommissionPayment,
        "Salary" or "SalaryPayment" => JournalEntrySource.SalaryPayment,
        _ => JournalEntrySource.System
    };
}
