using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

/// <summary>
/// عميل الشركة (تاجر تجزئة عادةً).
/// PlatformUserId / PlatformTraderId مرجع للمنصة الأم.
/// </summary>
public class Customer : BaseEntity
{
    public Guid PlatformUserId { get; private set; }      // مرجع لـ User من DB الأم
    public Guid PlatformTraderId { get; private set; }    // مرجع لـ Trader من DB الأم
    public string Code { get; private set; } = default!;
    public string BusinessName { get; private set; } = default!;
    public string OwnerName { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public decimal CreditLimit { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public int? AssignedSalesRepId { get; private set; }
    public int? AccountId { get; private set; }            // مرجع لحساب الذمم في acc.Accounts
    public bool IsActive { get; private set; }

    private Customer() { }

    public static Customer Create(Guid platformUserId, Guid platformTraderId,
                                   string code, string businessName, string ownerName, string phone)
    {
        return new Customer
        {
            PlatformUserId = platformUserId,
            PlatformTraderId = platformTraderId,
            Code = code, BusinessName = businessName, OwnerName = ownerName, Phone = phone,
            IsActive = true
        };
    }

    public void SetCreditLimit(decimal limit)
    {
        if (limit < 0) throw new DomainException("الحد الائتماني سالب");
        CreditLimit = limit;
    }

    public void AssignSalesRep(int salesRepId) => AssignedSalesRepId = salesRepId;
    public void LinkAccount(int accountId) => AccountId = accountId;

    public bool CanIssueInvoice(decimal newAmount)
        => CreditLimit == 0 || (CurrentBalance + newAmount) <= CreditLimit;

    public void AdjustBalance(decimal delta) => CurrentBalance += delta;
}
