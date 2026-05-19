namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;

public class FiscalYearDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedAt { get; set; }
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
