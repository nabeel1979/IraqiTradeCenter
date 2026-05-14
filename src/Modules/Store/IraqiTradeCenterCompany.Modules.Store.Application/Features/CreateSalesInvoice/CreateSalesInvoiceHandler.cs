using System.Transactions;
using AutoMapper;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts;
using IraqiTradeCenterCompany.Modules.Inventory.Application.Contracts.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Store.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Store.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Store.Application.Features.CreateSalesInvoice;

/// <summary>
/// المعالج الأهم في النظام - يربط Store + Accounting + Inventory في عملية واحدة.
/// 
/// التسلسل:
///   1) تحقق من العميل والائتمان
///   2) جلب صورة كل مادة من Inventory (IInventoryService)
///   3) بناء الفاتورة + إصدارها
///   4) خصم المخزون (IInventoryService.RecordSalesOutAsync) لكل سطر
///   5) تعديل رصيد العميل
///   6) إنشاء قيد محاسبي تلقائي (IAccountingService)
///   7) ربط القيد بالفاتورة
///   8) إذا الفاتورة من طلبية → تأكيد الطلبية
/// 
/// كل ذلك داخل TransactionScope واحد.
/// </summary>
public class CreateSalesInvoiceHandler : IRequestHandler<CreateSalesInvoiceCommand, Result<SalesInvoiceDto>>
{
    private readonly IStoreDbContext _store;
    private readonly IAccountingService _accounting;
    private readonly IInventoryService _inventory;
    private readonly IMapper _mapper;

    // الحسابات المستخدمة (موجودة في شجرة الحسابات بعد Seed)
    private const string AR_AccountCode = "1.2.01";          // ذمم العملاء (مدين)
    private const string Revenue_AccountCode = "4.1.01";     // إيرادات المبيعات (دائن)
    private const string TaxPayable_AccountCode = "2.1.02";  // ضريبة مستحقة (دائن)
    private const string Discount_AccountCode = "4.1.02";    // خصومات ممنوحة (مدين)

    public CreateSalesInvoiceHandler(IStoreDbContext store, IAccountingService accounting,
        IInventoryService inventory, IMapper mapper)
    {
        _store = store; _accounting = accounting; _inventory = inventory; _mapper = mapper;
    }

    public async Task<Result<SalesInvoiceDto>> Handle(CreateSalesInvoiceCommand req, CancellationToken ct)
    {
        // TransactionScope لضمان atomicity عبر الـ DbContexts الثلاث
        using var scope = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
            TransactionScopeAsyncFlowOption.Enabled);

        try
        {
            // 1) العميل
            var customer = await _store.Customers.FirstOrDefaultAsync(c => c.Id == req.CustomerId, ct);
            if (customer == null) return Result.Failure<SalesInvoiceDto>("العميل غير موجود");
            if (!customer.IsActive) return Result.Failure<SalesInvoiceDto>("العميل غير مفعّل");

            // 2) التأكد من الفترة المحاسبية مفتوحة
            await _accounting.EnsurePeriodOpenAsync(DateTime.UtcNow.Date, ct);

            // 3) جلب snapshots للمواد
            var itemIds = req.Lines.Select(l => l.ItemId).Distinct().ToList();
            var snapshots = new Dictionary<int, ItemSnapshot>();
            foreach (var id in itemIds)
            {
                var snap = await _inventory.GetItemSnapshotAsync(id, ct);
                if (snap == null) return Result.Failure<SalesInvoiceDto>($"المادة {id} غير موجودة");
                if (!snap.IsAvailableForSale) return Result.Failure<SalesInvoiceDto>($"المادة {snap.NameAr} غير متاحة للبيع");
                snapshots[id] = snap;
            }

            // 4) التحقق من المخزون لكل سطر
            foreach (var l in req.Lines)
            {
                var ok = await _inventory.CheckStockAvailabilityAsync(l.ItemId, l.UnitOfMeasureId, l.Quantity, ct);
                if (!ok)
                    return Result.Failure<SalesInvoiceDto>(
                        $"المخزون غير كافٍ للمادة '{snapshots[l.ItemId].NameAr}'");
            }

            // 5) بناء الفاتورة
            var invoice = SalesInvoice.Create(req.CustomerId, req.SalesRepId, req.TaxRate, req.IncomingOrderId);
            foreach (var l in req.Lines)
            {
                var snap = snapshots[l.ItemId];
                var (unitName, factor, defaultPrice) = ResolveUnit(snap, l.UnitOfMeasureId);
                var price = l.UnitPriceOverride ?? defaultPrice;
                invoice.AddLine(l.ItemId, snap.NameAr, l.UnitOfMeasureId, unitName,
                    l.Quantity, factor, price, l.LineDiscount);
            }

            if (req.DiscountPercentage > 0 || req.DiscountAmount > 0)
                invoice.ApplyDiscount(req.DiscountPercentage, req.DiscountAmount);

            // 6) فحص حد الائتمان
            if (!customer.CanIssueInvoice(invoice.TotalAmount))
                return Result.Failure<SalesInvoiceDto>(
                    $"تجاوز الحد الائتماني للعميل. الحد: {customer.CreditLimit:N0} | الرصيد: {customer.CurrentBalance:N0} | الفاتورة: {invoice.TotalAmount:N0}");

            invoice.Issue();
            await _store.SalesInvoices.AddAsync(invoice, ct);
            await _store.SaveChangesAsync(ct);

            // 7) خصم المخزون لكل سطر (عبر Contract)
            var defaultWh = await _inventory.GetDefaultWarehouseIdAsync(ct)
                ?? throw new DomainException("لا يوجد مخزن افتراضي معرف");
            foreach (var line in invoice.Lines)
            {
                await _inventory.RecordSalesOutAsync(new StockOutRequest
                {
                    ItemId = line.ItemId,
                    WarehouseId = defaultWh,
                    UnitOfMeasureId = line.UnitOfMeasureId,
                    Quantity = line.Quantity,
                    ReferenceType = "SalesInvoice",
                    ReferenceId = invoice.Id,
                    ReferenceNumber = invoice.InvoiceNumber
                }, ct);
            }

            // 8) تعديل رصيد العميل
            customer.AdjustBalance(invoice.TotalAmount);

            // 9) إنشاء قيد محاسبي تلقائي (عبر Contract)
            var entryLines = new List<AutomaticEntryLine>
            {
                new() { AccountCode = AR_AccountCode, IsDebit = true, Amount = invoice.TotalAmount,
                        Description = $"ذمم العميل {customer.BusinessName}" },
                new() { AccountCode = Revenue_AccountCode, IsDebit = false, Amount = invoice.SubTotal - invoice.DiscountAmount,
                        Description = "إيرادات مبيعات" }
            };
            if (invoice.TaxAmount > 0)
                entryLines.Add(new AutomaticEntryLine { AccountCode = TaxPayable_AccountCode, IsDebit = false,
                                                        Amount = invoice.TaxAmount, Description = "ضريبة" });

            var entryId = await _accounting.CreateAutomaticJournalEntryAsync(new CreateAutomaticEntryRequest
            {
                EntryDate = invoice.InvoiceDate,
                SourceCode = "SalesInvoice",
                Description = $"فاتورة مبيعات {invoice.InvoiceNumber}",
                ReferenceType = "SalesInvoice", ReferenceId = invoice.Id, ReferenceNumber = invoice.InvoiceNumber,
                Lines = entryLines
            }, ct);

            invoice.LinkJournalEntry(entryId);

            // 10) إذا من طلبية - أكدها
            if (req.IncomingOrderId.HasValue)
            {
                var order = await _store.IncomingOrders.FirstOrDefaultAsync(o => o.Id == req.IncomingOrderId, ct);
                if (order != null) order.Confirm(invoice.Id);
            }

            await _store.SaveChangesAsync(ct);

            scope.Complete();

            // إعادة جلب الفاتورة مع الأسطر
            var saved = await _store.SalesInvoices.AsNoTracking()
                .Include(i => i.Lines).FirstAsync(i => i.Id == invoice.Id, ct);
            return Result.Success(_mapper.Map<SalesInvoiceDto>(saved));
        }
        catch (DomainException ex) { return Result.Failure<SalesInvoiceDto>(ex.Message); }
    }

    private static (string UnitName, decimal Factor, decimal Price) ResolveUnit(ItemSnapshot snap, int unitId)
    {
        if (unitId == snap.BaseUnitId)
            return (snap.BaseUnitName, 1m, snap.BaseSalesPrice);
        if (unitId == snap.MediumUnitId && snap.MediumUnitFactor.HasValue)
            return (snap.MediumUnitName!, snap.MediumUnitFactor.Value, snap.MediumSalesPrice ?? snap.BaseSalesPrice * snap.MediumUnitFactor.Value);
        if (unitId == snap.LargeUnitId && snap.LargeUnitFactor.HasValue && snap.MediumUnitFactor.HasValue)
        {
            var factor = snap.LargeUnitFactor.Value * snap.MediumUnitFactor.Value;
            return (snap.LargeUnitName!, factor, snap.LargeSalesPrice ?? snap.BaseSalesPrice * factor);
        }
        throw new DomainException($"وحدة قياس غير صالحة للمادة {snap.NameAr}");
    }
}
