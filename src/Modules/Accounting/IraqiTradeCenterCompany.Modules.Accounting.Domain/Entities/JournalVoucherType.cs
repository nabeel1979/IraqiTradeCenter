using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// نوع سند/قيد محاسبي قابل للتخصيص (سند قبض، سند دفع، سند تسوية، …).
/// يربط نوع السند بحسابات افتراضية للمدين/الدائن من الدليل المحاسبي،
/// لتسريع إنشاء القيود حين يختار المستخدم النوع.
/// </summary>
public class JournalVoucherType : BaseEntity
{
    /// <summary>كود مختصر فريد (مثل RV لسند قبض، PV لسند دفع، AV لسند تسوية)</summary>
    public string Code { get; private set; } = default!;
    public string NameAr { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public string? Description { get; private set; }

    /// <summary>الحساب الافتراضي للطرف المدين (اختياري)</summary>
    public int? DefaultDebitAccountId { get; private set; }

    /// <summary>الحساب الافتراضي للطرف الدائن (اختياري)</summary>
    public int? DefaultCreditAccountId { get; private set; }

    /// <summary>هل النوع متاح للاستخدام في القيود الجديدة؟</summary>
    public bool IsEnabled { get; private set; } = true;

    /// <summary>هل النوع مدمج بالنظام (لا يمكن حذفه)؟</summary>
    public bool IsSystem { get; private set; }

    /// <summary>ترتيب العرض في قوائم الاختيار</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>طبيعة السند: مدين / دائن / مختلط — تُستخدم في صفحة السند المستقل</summary>
    public VoucherNature Nature { get; private set; } = VoucherNature.Mixed;

    /// <summary>هل يظهر هذا النوع كصفحة مستقلة في القائمة الجانبية؟</summary>
    public bool ShowInSidebar { get; private set; }

    public virtual Account? DefaultDebitAccount { get; private set; }
    public virtual Account? DefaultCreditAccount { get; private set; }

    private JournalVoucherType() { }

    public static JournalVoucherType Create(
        string code,
        string nameAr,
        string? nameEn = null,
        string? description = null,
        int? defaultDebitAccountId = null,
        int? defaultCreditAccountId = null,
        bool isEnabled = true,
        bool isSystem = false,
        int displayOrder = 100,
        VoucherNature nature = VoucherNature.Mixed,
        bool showInSidebar = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainException("كود نوع السند مطلوب");
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new DomainException("الاسم العربي لنوع السند مطلوب");
        if (code.Length > 20)
            throw new DomainException("كود نوع السند طويل (1–20 حرف)");

        return new JournalVoucherType
        {
            Code = code.Trim().ToUpperInvariant(),
            NameAr = nameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DefaultDebitAccountId = defaultDebitAccountId,
            DefaultCreditAccountId = defaultCreditAccountId,
            IsEnabled = isEnabled,
            IsSystem = isSystem,
            DisplayOrder = displayOrder,
            Nature = nature,
            ShowInSidebar = showInSidebar,
        };
    }

    public void Update(
        string nameAr,
        string? nameEn,
        string? description,
        int? defaultDebitAccountId,
        int? defaultCreditAccountId,
        bool isEnabled,
        int displayOrder,
        VoucherNature nature,
        bool showInSidebar)
    {
        if (string.IsNullOrWhiteSpace(nameAr))
            throw new DomainException("الاسم العربي لنوع السند مطلوب");

        NameAr = nameAr.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DefaultDebitAccountId = defaultDebitAccountId;
        DefaultCreditAccountId = defaultCreditAccountId;
        IsEnabled = isEnabled;
        DisplayOrder = displayOrder;
        Nature = nature;
        ShowInSidebar = showInSidebar;
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void SetDisplayOrder(int order) => DisplayOrder = order;
}
