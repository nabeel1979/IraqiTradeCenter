using System.Transactions;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.CalculateCommission;

public class CalculateCommissionHandler : IRequestHandler<CalculateCommissionCommand, Result<int>>
{
    private readonly IStoreDbContext _store;
    private readonly IAccountingService _accounting;
    private const string CommissionExpense = "5.2.01";
    private const string CommissionsPayable = "2.1.03";

    public CalculateCommissionHandler(IStoreDbContext store, IAccountingService accounting)
    {
        _store = store; _accounting = accounting;
    }

    public async Task<Result<int>> Handle(CalculateCommissionCommand req, CancellationToken ct)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            var rep = await _store.SalesReps.Include(r => r.Tiers)
                .FirstOrDefaultAsync(r => r.Id == req.SalesRepId, ct);
            if (rep == null) return Result.Failure<int>("المندوب غير موجود");

            // إجمالي مبيعات المندوب في الفترة (فواتير مصدرة أو مدفوعة - لا ملغاة ولا مسودات)
            var totalSales = await _store.SalesInvoices
                .Where(i => i.SalesRepId == req.SalesRepId
                         && i.InvoiceDate >= req.FromDate && i.InvoiceDate <= req.ToDate
                         && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft)
                .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m;

            if (totalSales <= 0)
                return Result.Failure<int>("لا توجد مبيعات للمندوب في هذه الفترة");

            var commission = rep.CalculateCommission(totalSales);
            if (commission <= 0)
                return Result.Failure<int>("العمولة المحسوبة = 0");

            var trans = CommissionTransaction.Create(rep.Id, req.FromDate, req.ToDate, totalSales, commission);
            await _store.CommissionTransactions.AddAsync(trans, ct);
            await _store.SaveChangesAsync(ct);

            // قيد محاسبي: مدين مصروف عمولات، دائن عمولات مستحقة
            var entryId = await _accounting.CreateAutomaticJournalEntryAsync(new CreateAutomaticEntryRequest
            {
                EntryDate = DateTime.UtcNow.Date,
                SourceCode = "Commission",
                Description = $"عمولة المندوب {rep.FullName} عن الفترة {req.FromDate:yyyy-MM-dd} إلى {req.ToDate:yyyy-MM-dd}",
                ReferenceType = "Commission", ReferenceId = trans.Id,
                Lines = new()
                {
                    new() { AccountCode = CommissionExpense, IsDebit = true, Amount = commission,
                            Description = $"عمولة {rep.FullName}" },
                    new() { AccountCode = CommissionsPayable, IsDebit = false, Amount = commission,
                            Description = "مستحق دفعه" }
                }
            }, ct);

            trans.MarkAsPaid(entryId);
            await _store.SaveChangesAsync(ct);

            scope.Complete();
            return Result.Success(trans.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
