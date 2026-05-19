using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// سطر داخل نشرة الأسعار: العملة الأجنبية + سعرها + نوع العملية (ضرب/قسمة).
/// </summary>
public class CurrencyRateLine : BaseEntity
{
    public int CurrencyRateBulletinId { get; private set; }
    public string Currency { get; private set; } = default!;
    public decimal Rate { get; private set; }
    public CurrencyRateOperation Operation { get; private set; }
    public string? Notes { get; private set; }

    private CurrencyRateLine() { }

    internal static CurrencyRateLine Create(string currency, decimal rate, CurrencyRateOperation operation, string? notes)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new DomainException("العملة مطلوبة");
        if (rate <= 0)
            throw new DomainException("سعر العملة لازم موجب");

        return new CurrencyRateLine
        {
            Currency = currency.Trim().ToUpperInvariant(),
            Rate = rate,
            Operation = operation,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    /// <summary>يطبّق المعادلة على المبلغ.</summary>
    public decimal Convert(decimal amount)
        => Operation == CurrencyRateOperation.Multiply ? amount * Rate : amount / Rate;
}
