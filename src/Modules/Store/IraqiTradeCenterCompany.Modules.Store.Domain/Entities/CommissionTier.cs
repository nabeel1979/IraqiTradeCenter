using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Store.Domain.Entities;

public class CommissionTier : BaseEntity
{
    public int SalesRepId { get; private set; }
    public decimal FromSalesAmount { get; private set; }
    public decimal ToSalesAmount { get; private set; }
    public decimal CommissionRate { get; private set; }

    private CommissionTier() { }

    internal static CommissionTier Create(int repId, decimal from, decimal to, decimal rate)
    {
        if (to <= from) throw new DomainException("نهاية الشريحة أكبر من بدايتها");
        if (rate < 0 || rate > 100) throw new DomainException("النسبة بين 0 و 100");
        return new CommissionTier { SalesRepId = repId, FromSalesAmount = from, ToSalesAmount = to, CommissionRate = rate };
    }
}
