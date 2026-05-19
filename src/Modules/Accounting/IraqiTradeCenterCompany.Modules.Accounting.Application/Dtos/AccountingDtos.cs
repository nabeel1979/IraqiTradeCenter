using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;

public class AccountDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public AccountType Type { get; set; }
    public AccountNature Nature { get; set; }
    public int? ParentId { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<AccountDto> Children { get; set; } = new();
}

public class JournalEntryDto
{
    public int Id { get; set; }
    public string EntryNumber { get; set; } = default!;
    public DateTime EntryDate { get; set; }
    public string Status { get; set; } = default!;
    public string EntryType { get; set; } = "Normal";
    public string Currency { get; set; } = "IQD";
    public string Description { get; set; } = default!;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int? VoucherTypeId { get; set; }
    public string? VoucherTypeCode { get; set; }
    public string? VoucherTypeName { get; set; }
    public List<JournalLineDto> Lines { get; set; } = new();
}

public class JournalLineDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public bool IsDebit { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}

public class TrialBalanceRowDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// سطر في كشف الحساب
/// </summary>
public class AccountStatementRowDto
{
    public DateTime Date { get; set; }
    public string EntryNumber { get; set; } = default!;
    public int EntryId { get; set; }
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public string? Description { get; set; }
    public string? LineDescription { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>الرصيد التراكمي</summary>
    public decimal Balance { get; set; }
    /// <summary>الرصيد التراكمي مقوَّمًا بالعملة الأساسية</summary>
    public decimal BalanceValuated { get; set; }
    public string Currency { get; set; } = "IQD";

    /// <summary>نوع القيد (Normal | Opening) كنص</summary>
    public string EntryType { get; set; } = "Normal";
    /// <summary>مصدر القيد كنص (Manual, SalesInvoice, Payment, ...)</summary>
    public string Source { get; set; } = "Manual";
    /// <summary>نوع المرجع لأصل القيد (إن وُجد) — مثلاً SalesInvoice</summary>
    public string? ReferenceType { get; set; }
    /// <summary>مُعرّف المرجع لأصل القيد (إن وُجد)</summary>
    public int? ReferenceId { get; set; }
    /// <summary>رقم/كود المرجع لأصل القيد (إن وُجد)</summary>
    public string? ReferenceNumber { get; set; }
}

/// <summary>
/// كشف حساب كامل (لحساب واحد أو لجميع الحسابات)
/// </summary>
public class AccountStatementDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    /// <summary>فلتر العرض (جميع أو عملة واحدة في القيد)</summary>
    public string Currency { get; set; } = "IQD";
    /// <summary>العملة المستخدمة في التقييم</summary>
    public string BaseCurrency { get; set; } = "IQD";
    /// <summary>true إذا لم يجد سعرًا لعملة واحدة على الأقل واستعملنا مضاعف 1</summary>
    public bool FxUsedFallback { get; set; }
    /// <summary>اسم النشرة المنشورة المستخدَمة في التقويم (إن وُجدت)</summary>
    public string? FxBulletinName { get; set; }
    /// <summary>تاريخ سريان النشرة المستخدَمة في التقويم (إن وُجدت)</summary>
    public DateTime? FxBulletinEffectiveAt { get; set; }
    /// <summary>true إذا كان كشف موحَّد لكل الحسابات</summary>
    public bool IsAllAccounts { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal OpeningBalanceValuated { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    /// <summary>إجمالي المدين بالعملة الأساسية</summary>
    public decimal TotalDebitValuated { get; set; }
    /// <summary>إجمالي الدائن بالعملة الأساسية</summary>
    public decimal TotalCreditValuated { get; set; }
    /// <summary>الرصيد الختامي بالعملة الأساسية — ما يُجمع تقريرياً</summary>
    public decimal ClosingBalanceValuated { get; set; }

    /// <summary>
    /// الرصيد الافتتاحي صافٍ لكل عملة (Code → صافي بالعملة المحلية).
    /// يحتوي مجموع (المدين − الدائن) للقيود التي تنتمي للرصيد الافتتاحي
    /// (Normal قبل الفترة + كل قيود Opening حتى نهاية الفترة).
    /// </summary>
    public Dictionary<string, decimal> OpeningByCurrency { get; set; } = new();

    /// <summary>
    /// مُضاعِفات التحويل لكل عملة من عملة السطر إلى العملة الأساسية،
    /// مأخوذة من نشرة الأسعار المستعملة. Code → multiplier.
    /// </summary>
    public Dictionary<string, decimal> CurrencyMultipliers { get; set; } = new();

    public List<AccountStatementRowDto> Rows { get; set; } = new();
}
