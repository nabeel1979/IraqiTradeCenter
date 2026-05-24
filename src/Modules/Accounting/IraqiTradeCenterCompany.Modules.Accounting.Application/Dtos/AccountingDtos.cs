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
    /// <summary>
    /// هل الحساب مفعَّل؟ الحسابات المعطَّلة (IsActive=false) لا تظهر في شاشات
    /// اختيار الحسابات (قيود/صناديق/سندات…) لكن كودها يبقى محجوزاً في قاعدة
    /// البيانات، لذلك يجب إظهارها في شاشة شجرة الحسابات حتى يعرف المستخدم لماذا
    /// لا يستطيع إعادة استخدام كود معيّن.
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// هل الحساب مرتبط بسطر قيد محاسبي، أو بصندوق، أو بنوع سند (كحساب افتراضي مدين/دائن)؟
    /// عندما يكون <c>true</c> يجب على الواجهة حجب أزرار إضافة الفروع والحذف لتفادي
    /// كسر السلامة المرجعية. الحماية الأساسية تبقى في الـ Handlers على الخادم.
    /// </summary>
    public bool IsUsed { get; set; }
    public List<AccountDto> Children { get; set; } = new();
}

/// <summary>
/// مدخل في سلة مهملات شجرة الحسابات — تمثيل مسطّح (بدون أبناء) مع سياق الأب.
/// </summary>
public class TrashedAccountDto
{
    public int Id { get; set; }
    public string Code { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public AccountType Type { get; set; }
    public AccountNature Nature { get; set; }
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public int? ParentId { get; set; }
    public string? ParentCode { get; set; }
    public string? ParentNameAr { get; set; }
    /// <summary>هل الأب نفسه ما زال محذوفاً؟ في هذه الحالة لا يمكن الاستعادة قبل استعادته.</summary>
    public bool ParentIsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
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
    /// <summary>التسلسل الخاص بنوع السند (يبدأ من 1 لكل نوع)</summary>
    public int? VoucherSequence { get; set; }
    /// <summary>رقم السند المُهيّأ للعرض: "{Code}-{Sequence}" مثل "PV-1"</summary>
    public string? VoucherNumber { get; set; }
    /// <summary>مصدر القيد: Manual / SalesInvoice / PurchaseInvoice / Payment / Receipt / …</summary>
    public string Source { get; set; } = "Manual";
    /// <summary>المرجع المصدر (للقيود المولّدة من فواتير أو حركات أخرى)</summary>
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
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

/// <summary>
/// سطر ميزان مراجعة موسَّع — يتضمَّن أرصدة الفترة السابقة (افتتاحي مدين/دائن)،
/// حركة الفترة الحالية، والرصيد النهائي مقسَّماً لمدين/دائن.
/// </summary>
public class TrialBalanceRowDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    /// <summary>نوع الحساب: Asset/Liability/Equity/Revenue/Expense</summary>
    public string AccountType { get; set; } = default!;
    /// <summary>طبيعة الحساب: Debit/Credit</summary>
    public string AccountNature { get; set; } = default!;
    public int Level { get; set; }
    public bool IsLeaf { get; set; }
    public int? ParentId { get; set; }

    // ── الفترة السابقة (الافتتاحي قبل تاريخ "من")
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }

    // ── الفترة الحالية (الحركة بين "من" و "إلى")
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }

    // ── الرصيد النهائي مقسَّم لجانبَي الميزان
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

/// <summary>
/// تقرير ميزان المراجعة الكامل (صفوف + إجماليات + معلومات التقويم/النشرة).
/// </summary>
public class TrialBalanceDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>فلتر العملة المُطبَّق ("" = الكل)</summary>
    public string? Currency { get; set; }
    /// <summary>true = الأرقام مُقوَّمة بالعملة الأساسية</summary>
    public bool Valuated { get; set; }
    /// <summary>العملة الأساسية المستخدَمة في التقويم</summary>
    public string BaseCurrency { get; set; } = "IQD";
    /// <summary>اسم النشرة المستخدَمة في التقويم (إن وُجدت)</summary>
    public string? FxBulletinName { get; set; }
    /// <summary>تاريخ سريان النشرة (إن وُجدت)</summary>
    public DateTime? FxBulletinEffectiveAt { get; set; }
    /// <summary>true إذا لم نجد سعراً لعملة واحدة وتمّ استعمال مضاعف 1</summary>
    public bool FxUsedFallback { get; set; }

    public int? MaxLevel { get; set; }
    public bool LeavesOnly { get; set; }

    public List<TrialBalanceRowDto> Rows { get; set; } = new();

    // ── الإجماليات
    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }
    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }
    public decimal TotalClosingDebit { get; set; }
    public decimal TotalClosingCredit { get; set; }

    /// <summary>الفرق بين الإيرادات والمصاريف خلال الفترة (نتيجة الفترة قبل الضرائب)</summary>
    public decimal NetIncome { get; set; }
    /// <summary>إجمالي حركة حسابات الإيرادات في الفترة الحالية (دائن − مدين)</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>إجمالي حركة حسابات المصاريف في الفترة الحالية (مدين − دائن)</summary>
    public decimal TotalExpense { get; set; }
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
    /// <summary>رقم السند المُهيّأ للعرض ("PV-1") — null للقيود اليدوية</summary>
    public string? VoucherNumber { get; set; }
    /// <summary>رمز نوع السند ("PV", "RV", "JV") — null للقيود اليدوية</summary>
    public string? VoucherTypeCode { get; set; }
    /// <summary>التسلسل ضمن نوع السند (يبدأ من 1 لكل نوع)</summary>
    public int? VoucherSequence { get; set; }
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

    /// <summary>
    /// تفاصيل قيود الافتتاح (EntryType=2) التي تخص الحساب وتقع حتى نهاية الفترة.
    /// تُعرض في رأس الكشف بشكل واضح ليرى المستخدم مصدر الرصيد الافتتاحي،
    /// ولا تظهر بين الحركات لمنع الازدواجية مع OpeningBalance.
    /// </summary>
    public List<OpeningEntryRowDto> OpeningEntries { get; set; } = new();

    public List<AccountStatementRowDto> Rows { get; set; } = new();
}

/// <summary>سطر قيد افتتاح مفصَّل (يُعرض في رأس كشف الحساب).</summary>
public class OpeningEntryRowDto
{
    public int EntryId { get; set; }
    public string EntryNumber { get; set; } = default!;
    public DateTime EntryDate { get; set; }
    public string Currency { get; set; } = "IQD";
    public string? Description { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    /// <summary>صافي السطر (مدين − دائن) بعملة القيد.</summary>
    public decimal Net { get; set; }
    /// <summary>صافي السطر بالعملة الأساسية بعد التقويم.</summary>
    public decimal NetValuated { get; set; }
}
