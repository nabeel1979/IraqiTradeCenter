using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class Account : BaseEntity
{
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public AccountType Type { get; private set; }
    public AccountNature Nature { get; private set; }
    public int? ParentId { get; private set; }
    public int Level { get; private set; }
    public bool IsLeaf { get; private set; }
    public bool IsActive { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public string? Description { get; private set; }
    public virtual Account? Parent { get; private set; }
    public virtual ICollection<Account> Children { get; private set; } = new List<Account>();

    private Account() { }

    public static Account Create(string code, string nameAr, AccountType type, AccountNature nature,
                                  int? parentId, int level, bool isLeaf)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("رمز الحساب مطلوب");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("اسم الحساب مطلوب");
        return new Account
        {
            Code = code.Trim(), NameAr = nameAr.Trim(),
            Type = type, Nature = nature,
            ParentId = parentId, Level = level, IsLeaf = isLeaf,
            IsActive = true
        };
    }

    public static AccountNature GetDefaultNature(AccountType type) => type switch
    {
        AccountType.Asset or AccountType.Expense => AccountNature.Debit,
        _ => AccountNature.Credit
    };

    public void SetOpeningBalance(decimal b) => OpeningBalance = b;
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public void UpdateBasic(string nameAr, string? nameEn, string? description)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("اسم الحساب مطلوب");
        NameAr = nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void ChangeType(AccountType type, AccountNature nature)
    {
        Type = type;
        Nature = nature;
    }

    public void MarkAsLeaf(bool isLeaf) => IsLeaf = isLeaf;
}
