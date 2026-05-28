using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

public class FiscalYear : BaseEntity
{
    public string Name { get; private set; } = default!;
    /// <summary>
    /// الاسم الإنجليزي الاختياري للسنة المالية — يُعرض في واجهة اللغة الإنجليزية،
    /// وإلا يُستخدم الاسم العربي كـ fallback.
    /// </summary>
    public string? NameEn { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    /// <summary>
    /// السنة المالية المفعَّلة (النشطة). يجب ضمان وجود سنة واحدة فقط مفعَّلة
    /// في كل وقت من خلال الـ Application layer (الـ Command Handler).
    /// التقارير والشاشات الافتراضية تعتمد عليها.
    /// </summary>
    public bool IsActive { get; private set; }
    public virtual ICollection<AccountingPeriod> Periods { get; private set; } = new List<AccountingPeriod>();

    private FiscalYear() { }

    public static FiscalYear Create(string name, DateTime startDate, DateTime endDate, string? nameEn = null)
    {
        if (endDate <= startDate) throw new DomainException("تاريخ النهاية يجب أن يكون بعد البداية");
        var fy = new FiscalYear
        {
            Name = name.Trim(),
            NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim(),
            StartDate = startDate,
            EndDate = endDate,
        };
        BuildPeriods(fy);
        return fy;
    }

    /// <summary>تحديث الاسم والتواريخ. لا يُسمح به للسنة المغلقة.</summary>
    public void Update(string name, DateTime startDate, DateTime endDate, string? nameEn = null)
    {
        if (IsClosed) throw new DomainException("لا يمكن تعديل سنة مالية مغلقة");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("اسم السنة المالية مطلوب");
        if (endDate <= startDate) throw new DomainException("تاريخ النهاية يجب أن يكون بعد البداية");

        Name = name.Trim();
        NameEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    /// <summary>
    /// يُعيد توليد الفترات الشهرية بناءً على تواريخ السنة الحالية.
    /// المستدعي مسؤول عن حذف الفترات القديمة من الـ DbContext أولاً.
    /// </summary>
    public void RegeneratePeriods()
    {
        Periods.Clear();
        BuildPeriods(this);
    }

    private static void BuildPeriods(FiscalYear fy)
    {
        var current = fy.StartDate; int n = 1;
        while (current < fy.EndDate)
        {
            var pEnd = current.AddMonths(1).AddDays(-1);
            if (pEnd > fy.EndDate) pEnd = fy.EndDate;
            fy.Periods.Add(AccountingPeriod.Create(n++, current, pEnd));
            current = current.AddMonths(1);
        }
    }

    public void Close() { IsClosed = true; ClosedAt = DateTime.UtcNow; }

    /// <summary>
    /// فك إغلاق السنة المالية وإعادة فترَاتها إلى حالة "مفتوحة" — يسمح
    /// مجدداً بإنشاء/تعديل/حذف القيود ضمن نطاقها. يجب على المستدعي التأكّد
    /// من أن السنة التالية ليست مدوَّرة منها (الإقفال مع تدوير لا يُعكس
    /// تلقائياً — يجب حذف قيود التدوير يدوياً قبل فك الإغلاق).
    /// </summary>
    public void Reopen()
    {
        if (!IsClosed) throw new DomainException("السنة المالية ليست مغلقة");
        IsClosed = false;
        ClosedAt = null;
        foreach (var p in Periods) p.ForceOpen();
    }

    /// <summary>تفعيل السنة كنشطة. لا يُسمح بتفعيل سنة مغلقة.</summary>
    public void Activate()
    {
        if (IsClosed) throw new DomainException("لا يمكن تفعيل سنة مالية مغلقة");
        IsActive = true;
    }

    /// <summary>إلغاء تفعيل السنة (للسماح بتفعيل غيرها).</summary>
    public void Deactivate() => IsActive = false;
}
