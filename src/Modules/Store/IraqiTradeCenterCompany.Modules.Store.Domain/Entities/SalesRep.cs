using IraqiTradeCenterCompany.Modules.Store.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

/// <summary>
/// مندوب مبيعات - موظف داخل الشركة.
/// CommissionType: Fixed = نسبة ثابتة | Tiered = حسب الشرائح
/// </summary>
public class SalesRep : BaseEntity
{
    public Guid UserId { get; private set; }
    public string EmployeeCode { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public decimal BaseSalary { get; private set; }
    public CommissionType CommissionType { get; private set; }
    public decimal? FixedCommissionRate { get; private set; }
    public string? Region { get; private set; }
    public bool IsActive { get; private set; }

    public virtual ICollection<CommissionTier> Tiers { get; private set; } = new List<CommissionTier>();

    private SalesRep() { }

    public static SalesRep Create(Guid userId, string code, string fullName, string phone,
                                   decimal baseSalary, CommissionType type, decimal? fixedRate = null)
    {
        if (type == CommissionType.Fixed && !fixedRate.HasValue)
            throw new DomainException("عمولة ثابتة تتطلب تحديد النسبة");
        return new SalesRep
        {
            UserId = userId, EmployeeCode = code, FullName = fullName, Phone = phone,
            BaseSalary = baseSalary, CommissionType = type, FixedCommissionRate = fixedRate,
            IsActive = true
        };
    }

    public void AddTier(decimal fromAmount, decimal toAmount, decimal rate)
    {
        if (CommissionType != CommissionType.Tiered)
            throw new DomainException("الشرائح تخص العمولة المتدرجة فقط");
        Tiers.Add(CommissionTier.Create(Id, fromAmount, toAmount, rate));
    }

    public decimal CalculateCommission(decimal totalSales)
    {
        if (CommissionType == CommissionType.Fixed)
            return Math.Round(totalSales * (FixedCommissionRate ?? 0) / 100m, 3);

        decimal commission = 0;
        foreach (var t in Tiers.OrderBy(t => t.FromSalesAmount))
        {
            if (totalSales <= t.FromSalesAmount) break;
            var slice = Math.Min(totalSales, t.ToSalesAmount) - t.FromSalesAmount;
            if (slice > 0) commission += slice * t.CommissionRate / 100m;
        }
        return Math.Round(commission, 3);
    }
}
