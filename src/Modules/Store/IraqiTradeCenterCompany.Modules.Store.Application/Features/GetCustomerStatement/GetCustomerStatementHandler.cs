using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.GetCustomerStatement;

public class GetCustomerStatementHandler : IRequestHandler<GetCustomerStatementQuery, Result<CustomerStatementDto>>
{
    private readonly IStoreDbContext _store;
    public GetCustomerStatementHandler(IStoreDbContext store) => _store = store;

    public async Task<Result<CustomerStatementDto>> Handle(GetCustomerStatementQuery req, CancellationToken ct)
    {
        var customer = await _store.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CustomerId, ct);
        if (customer == null) return Result.Failure<CustomerStatementDto>("العميل غير موجود");

        var invoices = await _store.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == req.CustomerId && i.InvoiceDate >= req.FromDate && i.InvoiceDate <= req.ToDate
                     && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { Date = i.InvoiceDate, Type = "Invoice", Number = i.InvoiceNumber, i.TotalAmount })
            .ToListAsync(ct);

        var payments = await _store.PaymentsReceived.AsNoTracking()
            .Where(p => p.CustomerId == req.CustomerId && p.PaymentDate >= req.FromDate && p.PaymentDate <= req.ToDate)
            .Select(p => new { Date = p.PaymentDate, Type = "Payment", Number = p.ReceiptNumber, Amount = p.Amount })
            .ToListAsync(ct);

        var lines = invoices
            .Select(i => new CustomerStatementLineDto { Date = i.Date, DocType = i.Type, DocNumber = i.Number, Debit = i.TotalAmount })
            .Concat(payments.Select(p => new CustomerStatementLineDto { Date = p.Date, DocType = p.Type, DocNumber = p.Number, Credit = p.Amount }))
            .OrderBy(l => l.Date).ThenBy(l => l.DocType == "Invoice" ? 0 : 1)
            .ToList();

        decimal balance = 0;
        foreach (var l in lines) { balance += l.Debit - l.Credit; l.Balance = balance; }

        return Result.Success(new CustomerStatementDto
        {
            CustomerId = customer.Id, CustomerName = customer.BusinessName,
            FromDate = req.FromDate, ToDate = req.ToDate,
            OpeningBalance = 0, ClosingBalance = balance,
            Lines = lines
        });
    }
}
