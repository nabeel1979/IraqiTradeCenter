using System.Transactions;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.RecordPayment;

public class RecordPaymentHandler : IRequestHandler<RecordPaymentCommand, Result<int>>
{
    private readonly IStoreDbContext _store;
    private readonly IAccountingService _accounting;
    private const string AR_AccountCode = "1.2.01";
    private const string Cash_AccountCode = "1.1.01";
    private const string Bank_AccountCode = "1.1.02";

    public RecordPaymentHandler(IStoreDbContext store, IAccountingService accounting)
    {
        _store = store; _accounting = accounting;
    }

    public async Task<Result<int>> Handle(RecordPaymentCommand req, CancellationToken ct)
    {
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            var invoice = await _store.SalesInvoices.FirstOrDefaultAsync(i => i.Id == req.SalesInvoiceId, ct);
            if (invoice == null) return Result.Failure<int>("الفاتورة غير موجودة");
            var customer = await _store.Customers.FirstOrDefaultAsync(c => c.Id == invoice.CustomerId, ct);
            if (customer == null) return Result.Failure<int>("العميل غير موجود");

            await _accounting.EnsurePeriodOpenAsync(DateTime.UtcNow.Date, ct);

            // اختر حساب الكاش/البنك
            var cashCode = req.PaymentMethod.ToLowerInvariant() switch
            {
                "bank" or "transfer" => Bank_AccountCode,
                _ => Cash_AccountCode
            };
            var cashAccountId = await _accounting.GetAccountIdByCodeAsync(cashCode, ct);

            // سجل الدفع في الفاتورة
            invoice.RecordPayment(req.Amount);
            customer.AdjustBalance(-req.Amount);

            // أنشئ سجل الدفع
            var payment = PaymentReceived.Create(customer.Id, invoice.Id, req.Amount,
                req.PaymentMethod, cashAccountId, req.ReferenceNumber);
            await _store.PaymentsReceived.AddAsync(payment, ct);
            await _store.SaveChangesAsync(ct);

            // قيد محاسبي: مدين كاش/بنك، دائن ذمم
            var entryId = await _accounting.CreateAutomaticJournalEntryAsync(new CreateAutomaticEntryRequest
            {
                EntryDate = payment.PaymentDate,
                SourceCode = "Payment",
                Description = $"تحصيل من العميل {customer.BusinessName} - فاتورة {invoice.InvoiceNumber}",
                ReferenceType = "PaymentReceived", ReferenceId = payment.Id, ReferenceNumber = payment.ReceiptNumber,
                Lines = new()
                {
                    new() { AccountCode = cashCode, IsDebit = true, Amount = req.Amount,
                            Description = $"تحصيل {req.PaymentMethod}" },
                    new() { AccountCode = AR_AccountCode, IsDebit = false, Amount = req.Amount,
                            Description = $"تسديد {customer.BusinessName}" }
                }
            }, ct);

            payment.LinkJournalEntry(entryId);
            await _store.SaveChangesAsync(ct);

            scope.Complete();
            return Result.Success(payment.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
