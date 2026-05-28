namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;

public class FiscalYearDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    /// <summary>الاسم الإنجليزي الاختياري — يُعرض في الواجهة الإنجليزية.</summary>
    public string? NameEn { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    /// <summary>السنة المالية المفعَّلة — التقارير والشاشات الافتراضية تعتمد عليها.</summary>
    public bool IsActive { get; set; }
    public List<AccountingPeriodDto> Periods { get; set; } = new();
}

public class AccountingPeriodDto
{
    public int Id { get; set; }
    public int FiscalYearId { get; set; }
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = default!;
}

public class FiscalYearStatusDto
{
    public int FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = default!;
    public string? FiscalYearNameEn { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int TotalPeriods { get; set; }
    public int OpenPeriods { get; set; }
    public int ClosedPeriods { get; set; }
    public int LockedPeriods { get; set; }
    public int DraftEntries { get; set; }
    public int PostedEntries { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public bool IsBalanced { get; set; }
}

public class FiscalYearValidationDto
{
    public bool CanClose { get; set; }
    public List<string> Issues { get; set; } = new();
    public int DraftEntries { get; set; }
    public bool IsBalanced { get; set; }
    public decimal Difference { get; set; }
    /// <summary>تفاصيل القيود المسودة التي تمنع الإغلاق (ليتمكن المستخدم من فتحها/معالجتها).</summary>
    public List<DraftJournalEntryRefDto> DraftEntriesList { get; set; } = new();
}

/// <summary>إشارة مختصرة لقيد مسودة — تُستخدم لعرض روابط معالجة في صفحة الفترات.</summary>
public class DraftJournalEntryRefDto
{
    public int Id { get; set; }
    public string EntryNumber { get; set; } = default!;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = default!;
    /// <summary>كود نوع السند (مثل "RV") إن كان القيد مرتبطاً بسند، وإلا null للقيود اليدوية.</summary>
    public string? VoucherTypeCode { get; set; }
    public int? VoucherSequence { get; set; }
}

public class FiscalYearCloseResultDto
{
    public bool Success { get; set; }
    public int FiscalYearId { get; set; }
    public DateTime ClosedAt { get; set; }
    public int LockedPeriods { get; set; }
    public string Message { get; set; } = default!;
}

public class FiscalYearRolloverResultDto
{
    public bool Success { get; set; }
    public int FromFiscalYearId { get; set; }
    public int ToFiscalYearId { get; set; }
    public int BalanceSheetAccountsRolled { get; set; }
    public decimal RetainedEarningsTransferred { get; set; }
    public string Message { get; set; } = default!;
}

/// <summary>
/// نتيجة الاستعلام عن حالة الفترة المحاسبية بتاريخ معيّن — يستخدمها الـ frontend
/// ليتحكّم بإظهار أزرار الحفظ/التعديل/الحذف على صفحات إدخال القيود والسندات.
/// </summary>
public class PeriodStatusByDateDto
{
    public DateTime Date { get; set; }
    public int FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = default!;
    public bool FiscalYearIsClosed { get; set; }
    public int PeriodId { get; set; }
    public int PeriodNumber { get; set; }
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    /// <summary>1=مفتوحة، 2=مغلقة، 3=مقفلة</summary>
    public int PeriodStatus { get; set; }
    /// <summary>true إذا كان يمكن تسجيل/تعديل/حذف القيود في هذا التاريخ.</summary>
    public bool IsEditable { get; set; }
}
