using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// صندوق نقدي/خزينة. يربط حسابًا واحدًا من الدليل المحاسبي مع قائمة بالعملات
/// المسموحة، وحدود (سقف) دائنة/مدينة لكل عملة. يُستخدم في صفحات السندات
/// المستقلة (سند قبض / سند دفع) ويحدّد الصلاحيات والـ ledger للأرصدة.
/// </summary>
public class CashBox : BaseEntity
{
    /// <summary>كود مختصر مميّز (مثل CB-MAIN، CB-USD)</summary>
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }

    /// <summary>الحساب المربوط من الدليل المحاسبي (يجب أن يكون Leaf — يقبل قيوداً)</summary>
    public int AccountId { get; private set; }

    /// <summary>هل الصندوق مفعّل (يظهر للمستخدمين)؟</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>ترتيب العرض في القوائم</summary>
    public int DisplayOrder { get; private set; }

    public string? Description { get; private set; }

    public virtual Account? Account { get; private set; }
    public virtual ICollection<CashBoxCurrency> Currencies { get; private set; } = new List<CashBoxCurrency>();

    private CashBox() { }

    public static CashBox Create(
        string code,
        string nameAr,
        int accountId,
        string? nameEn = null,
        string? description = null,
        bool isActive = true,
        int displayOrder = 100)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("كود الصندوق مطلوب");
        if (code.Length > 30) throw new DomainException("كود الصندوق طويل (حد أقصى 30)");
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("الاسم العربي للصندوق مطلوب");
        if (accountId <= 0) throw new DomainException("حساب الصندوق مطلوب");

        return new CashBox
        {
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            AccountId = accountId,
            IsActive = isActive,
            DisplayOrder = displayOrder,
        };
    }

    public void Update(
        string nameAr,
        int accountId,
        string? nameEn,
        string? description,
        bool isActive,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(nameAr)) throw new DomainException("الاسم العربي للصندوق مطلوب");
        if (accountId <= 0) throw new DomainException("حساب الصندوق مطلوب");

        NameAr = nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AccountId = accountId;
        IsActive = isActive;
        DisplayOrder = displayOrder;
    }

    public void SetActive(bool active) => IsActive = active;
    public void SetDisplayOrder(int order) => DisplayOrder = order;
}

/// <summary>
/// عملة مدعومة في صندوق محدّد، مع حدود اختيارية للسقف المدين والدائن.
/// السقف يُنذر فقط (لا يمنع) ما لم يُفرض في القيود — يمكن استخدامه لاحقاً للتحقق.
/// </summary>
public class CashBoxCurrency : BaseEntity
{
    public int CashBoxId { get; private set; }
    public string Currency { get; private set; } = default!;

    /// <summary>السقف المدين (مبلغ موجب يحدّد أقصى رصيد مدين مسموح). null = بلا سقف.</summary>
    public decimal? DebitLimit { get; private set; }

    /// <summary>السقف الدائن (مبلغ موجب). null = بلا سقف.</summary>
    public decimal? CreditLimit { get; private set; }

    public bool IsActive { get; private set; } = true;

    public virtual CashBox? CashBox { get; private set; }

    private CashBoxCurrency() { }

    /// <summary>
    /// إنشاء سطر عملة. يُمرَّر cashBoxId = 0 عند إنشاء صندوق جديد لم يُحفظ بعد،
    /// وفي هذه الحالة EF يربطه تلقائياً عبر navigation property.
    /// </summary>
    public static CashBoxCurrency Create(int cashBoxId, string currency, decimal? debitLimit, decimal? creditLimit, bool isActive = true)
    {
        if (cashBoxId < 0) throw new DomainException("معرّف الصندوق غير صالح");
        if (string.IsNullOrWhiteSpace(currency)) throw new DomainException("رمز العملة مطلوب");
        if (debitLimit.HasValue && debitLimit.Value < 0) throw new DomainException("السقف المدين يجب أن يكون موجبًا");
        if (creditLimit.HasValue && creditLimit.Value < 0) throw new DomainException("السقف الدائن يجب أن يكون موجبًا");

        return new CashBoxCurrency
        {
            CashBoxId = cashBoxId,
            Currency = currency.Trim().ToUpperInvariant(),
            DebitLimit = debitLimit,
            CreditLimit = creditLimit,
            IsActive = isActive,
        };
    }

    public void Update(decimal? debitLimit, decimal? creditLimit, bool isActive)
    {
        if (debitLimit.HasValue && debitLimit.Value < 0) throw new DomainException("السقف المدين يجب أن يكون موجبًا");
        if (creditLimit.HasValue && creditLimit.Value < 0) throw new DomainException("السقف الدائن يجب أن يكون موجبًا");
        DebitLimit = debitLimit;
        CreditLimit = creditLimit;
        IsActive = isActive;
    }
}
