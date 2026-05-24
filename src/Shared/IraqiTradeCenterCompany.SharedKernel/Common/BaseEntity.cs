namespace IraqiTradeCenterCompany.SharedKernel.Common;

public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }

    public void MarkAsDeleted(string? by = null)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdatedBy = by;
    }

    /// <summary>
    /// إلغاء الحذف الناعم (يُستخدم في حالات نادرة كإحياء كيانات نظامية تُنشأ
    /// تلقائياً مع كود فريد قد سبق حذفه).
    /// </summary>
    public void Restore(string? by = null)
    {
        IsDeleted = false;
        DeletedAt = null;
        UpdatedBy = by;
    }
    public void SetCreated(string? by) { CreatedAt = DateTime.UtcNow; CreatedBy = by; }
    public void SetUpdated(string? by) { UpdatedAt = DateTime.UtcNow; UpdatedBy = by; }
}

public abstract class BaseEntityGuid
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public string? UpdatedBy { get; protected set; }
    public bool IsDeleted { get; protected set; }
}
