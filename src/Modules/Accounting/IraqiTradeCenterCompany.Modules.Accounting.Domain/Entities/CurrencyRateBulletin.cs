using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// نشرة أسعار صرف العملات لتحويل العملات الأجنبية إلى العملة الرئيسية.
/// النشرة المنشورة (Published) ذات أحدث EffectiveAt تُعدّ النشرة المعتمدة (الافتراضية).
/// </summary>
public class CurrencyRateBulletin : BaseEntity
{
    public string Name { get; private set; } = default!;
    /// <summary>العملة الرئيسية (Base) التي تُحوَّل إليها بقية العملات</summary>
    public string BaseCurrency { get; private set; } = default!;
    /// <summary>تاريخ ووقت سريان النشرة (UTC)</summary>
    public DateTime EffectiveAt { get; private set; }
    public CurrencyRateBulletinStatus Status { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? PublishedBy { get; private set; }
    public string? Notes { get; private set; }

    public virtual ICollection<CurrencyRateLine> Lines { get; private set; } = new List<CurrencyRateLine>();

    private CurrencyRateBulletin() { }

    public static CurrencyRateBulletin Create(string name, string baseCurrency, DateTime effectiveAt, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("اسم النشرة مطلوب");
        if (string.IsNullOrWhiteSpace(baseCurrency))
            throw new DomainException("العملة الرئيسية مطلوبة");

        return new CurrencyRateBulletin
        {
            Name = name.Trim(),
            BaseCurrency = baseCurrency.Trim().ToUpperInvariant(),
            EffectiveAt = effectiveAt.Kind == DateTimeKind.Utc ? effectiveAt : effectiveAt.ToUniversalTime(),
            Status = CurrencyRateBulletinStatus.Draft,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };
    }

    public void UpdateMeta(string name, string baseCurrency, DateTime effectiveAt, string? notes)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("اسم النشرة مطلوب");
        if (string.IsNullOrWhiteSpace(baseCurrency)) throw new DomainException("العملة الرئيسية مطلوبة");

        Name = name.Trim();
        BaseCurrency = baseCurrency.Trim().ToUpperInvariant();
        EffectiveAt = effectiveAt.Kind == DateTimeKind.Utc ? effectiveAt : effectiveAt.ToUniversalTime();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public CurrencyRateLine AddLine(string currency, decimal rate, CurrencyRateOperation operation, string? notes = null)
    {
        EnsureDraft();
        var line = CurrencyRateLine.Create(currency, rate, operation, notes);
        if (string.Equals(line.Currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("لا تُضاف العملة الرئيسية إلى الأسطر");
        if (Lines.Any(l => string.Equals(l.Currency, line.Currency, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"العملة {line.Currency} مكرّرة في النشرة");
        Lines.Add(line);
        return line;
    }

    public void ClearLines()
    {
        EnsureDraft();
        Lines.Clear();
    }

    public void Publish(string? by = null)
    {
        if (Status == CurrencyRateBulletinStatus.Archived)
            throw new DomainException("لا يمكن نشر نشرة مؤرشفة");
        if (Status == CurrencyRateBulletinStatus.Published)
            return;
        if (Lines.Count == 0)
            throw new DomainException("لا يمكن نشر نشرة بدون أسعار");
        Status = CurrencyRateBulletinStatus.Published;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = by;
    }

    public void Archive(string? by = null)
    {
        Status = CurrencyRateBulletinStatus.Archived;
        SetUpdated(by);
    }

    public void RevertToDraft()
    {
        if (Status == CurrencyRateBulletinStatus.Archived)
            throw new DomainException("لا يمكن إعادة نشرة مؤرشفة");
        Status = CurrencyRateBulletinStatus.Draft;
        PublishedAt = null;
        PublishedBy = null;
    }

    /// <summary>يحوّل مبلغاً بعملة ما إلى العملة الرئيسية وفق سطر النشرة.</summary>
    public decimal? ConvertToBase(string currency, decimal amount)
    {
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
            return amount;
        var line = Lines.FirstOrDefault(l => string.Equals(l.Currency, currency, StringComparison.OrdinalIgnoreCase));
        return line?.Convert(amount);
    }

    private void EnsureDraft()
    {
        if (Status != CurrencyRateBulletinStatus.Draft)
            throw new DomainException("لا يمكن تعديل نشرة بعد نشرها أو أرشفتها");
    }
}
