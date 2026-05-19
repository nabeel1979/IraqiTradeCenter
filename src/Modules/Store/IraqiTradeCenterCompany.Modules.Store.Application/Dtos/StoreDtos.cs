namespace IraqiTradeCenterCompany.Modules.Store.Application.Dtos;

public class CustomerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string BusinessName { get; set; } = default!;
    public string OwnerName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public int? AssignedSalesRepId { get; set; }
    public bool IsActive { get; set; }
}

public class SalesInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public DateTime InvoiceDate { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int? SalesRepId { get; set; }
    public string Status { get; set; } = default!;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int? JournalEntryId { get; set; }
    public List<SalesInvoiceLineDto> Lines { get; set; } = new();
}

public class SalesInvoiceLineDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public string UnitName { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalesRepDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string CommissionType { get; set; } = default!;
    public decimal? FixedCommissionRate { get; set; }
    public decimal BaseSalary { get; set; }
    public string? Region { get; set; }
}

public class SalesRepPerformanceDto
{
    public int SalesRepId { get; set; }
    public string FullName { get; set; } = default!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalSales { get; set; }
    public int InvoiceCount { get; set; }
    public decimal CalculatedCommission { get; set; }
}

public class CustomerStatementLineDto
{
    public DateTime Date { get; set; }
    public string DocType { get; set; } = default!;     // Invoice / Payment
    public string DocNumber { get; set; } = default!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public class CustomerStatementDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<CustomerStatementLineDto> Lines { get; set; } = new();
}

public class IncomingOrderDto
{
    public int Id { get; set; }
    public Guid PlatformOrderId { get; set; }
    public string PlatformOrderNumber { get; set; } = default!;
    public DateTime ReceivedAt { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string Status { get; set; } = default!;
    public decimal TotalAmount { get; set; }
    public int? AssignedSalesRepId { get; set; }
    public int? CreatedInvoiceId { get; set; }
    public List<IncomingOrderItemDto> Items { get; set; } = new();
}

public class IncomingOrderItemDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public int UnitOfMeasureId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
