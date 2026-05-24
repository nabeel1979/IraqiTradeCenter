namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// يحدد أي صناديق يستطيع المستخدم الوصول إليها (يدرجها في القائمة المنسدلة، ويستلم/يصرف منها).
/// إذا لم يكن للمستخدم أي UserCashBox: لا يمكنه استخدام أي صندوق (الواجهة تخفي القائمة).
/// SuperAdmin يتجاوز هذه القيود تلقائياً.
/// </summary>
public class UserCashBox
{
    public Guid UserId { get; set; }
    public int CashBoxId { get; set; }

    /// <summary>true = يستطيع تنفيذ سندات قبض (إدخال مال للصندوق).</summary>
    public bool CanReceive { get; set; } = true;

    /// <summary>true = يستطيع تنفيذ سندات دفع (إخراج مال من الصندوق).</summary>
    public bool CanPay { get; set; } = true;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public CompanyUser? User { get; set; }
}
