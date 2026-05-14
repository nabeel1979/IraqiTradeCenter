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
    public string Description { get; set; } = default!;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
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
