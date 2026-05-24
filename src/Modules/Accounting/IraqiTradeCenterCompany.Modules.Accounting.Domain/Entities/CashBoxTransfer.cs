using IraqiTradeCenterCompany.SharedKernel.Common;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;

namespace IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;

/// <summary>
/// حالة المناقلة بين صندوقَين.
/// </summary>
public enum CashBoxTransferStatus
{
    /// <summary>تم إرسال المبلغ ويُنتظَر استلامه — قيد الإرسال موجود، قيد الاستلام غير مولَّد بعد.</summary>
    PendingReceive = 0,

    /// <summary>تم الاستلام رسمياً — قيد الاستلام مُولَّد ومرحَّل (إن أمكن).</summary>
    Received = 1,

    /// <summary>أُلغيت المناقلة — تم عكس قيد الإرسال (الحساب الوسيط مغلق).</summary>
    Cancelled = 2,
}

/// <summary>
/// مناقلة نقدية بين صندوقَين بآلية موافقة (Two-step):
///   1) عند الإنشاء يُولَّد قيد الإرسال فقط (الصندوق المُرسِل دائن، الحساب الوسيط مدين)
///      وتدخل المناقلة في حالة <see cref="CashBoxTransferStatus.PendingReceive"/>.
///   2) عند موافقة أمين الصندوق المستلم على الاستلام (يمكن أن يكون مستخدماً مختلفاً)
///      يُولَّد قيد الاستلام (الصندوق المستلم مدين، الحساب الوسيط دائن) وتنتقل
///      الحالة إلى <see cref="CashBoxTransferStatus.Received"/>.
///   3) قبل الاستلام يمكن إلغاء المناقلة، فيُعكَس قيد الإرسال وتنتقل الحالة إلى
///      <see cref="CashBoxTransferStatus.Cancelled"/>.
/// </summary>
public class CashBoxTransfer : BaseEntity
{
    /// <summary>رقم المناقلة (متسلسل: TRF-1، TRF-2 …)</summary>
    public string TransferNumber { get; private set; } = default!;

    public int FromCashBoxId { get; private set; }
    public int ToCashBoxId { get; private set; }

    /// <summary>الحساب الوسيط (Cash in Transit) المستخدم في طرفَي القيدَين</summary>
    public int TransitAccountId { get; private set; }

    public string Currency { get; private set; } = "IQD";
    public decimal Amount { get; private set; }

    /// <summary>تاريخ ووقت إرسال المبلغ من الصندوق المُرسِل</summary>
    public DateTime SendDate { get; private set; }

    /// <summary>تاريخ ووقت استلام المبلغ المتوقَّع/الفعلي في الصندوق المستلم</summary>
    public DateTime ReceiveDate { get; private set; }

    public string? Description { get; private set; }
    public string? ReferenceNumber { get; private set; }

    /// <summary>القيد المحاسبي المتولِّد على تاريخ الإرسال (الصندوق المُرسِل)</summary>
    public int SendJournalEntryId { get; private set; }

    /// <summary>
    /// القيد المحاسبي المتولِّد على تاريخ الاستلام — يبقى null حتى موافقة
    /// أمين الصندوق المستلم على الاستلام.
    /// </summary>
    public int? ReceiveJournalEntryId { get; private set; }

    /// <summary>حالة المناقلة في دورة الحياة (انتظار/مستلَم/ملغى).</summary>
    public CashBoxTransferStatus Status { get; private set; } = CashBoxTransferStatus.PendingReceive;

    /// <summary>المستخدم الذي وافق على الاستلام (NULL قبل الاستلام).</summary>
    public string? ReceivedByUserId { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public string? ReceiveNotes { get; private set; }

    /// <summary>المستخدم الذي ألغى المناقلة (NULL إذا لم تُلغَ).</summary>
    public string? CancelledByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    /// <summary>
    /// قيد عكس الإرسال عند الإلغاء (يُغلق الحساب الوسيط ويُعيد المبلغ للصندوق المُرسِل).
    /// </summary>
    public int? ReversalJournalEntryId { get; private set; }

    public virtual CashBox? FromCashBox { get; private set; }
    public virtual CashBox? ToCashBox { get; private set; }
    public virtual Account? TransitAccount { get; private set; }
    public virtual JournalEntry? SendJournalEntry { get; private set; }
    public virtual JournalEntry? ReceiveJournalEntry { get; private set; }
    public virtual JournalEntry? ReversalJournalEntry { get; private set; }

    private CashBoxTransfer() { }

    /// <summary>
    /// إنشاء مناقلة جديدة في حالة "بانتظار الاستلام" — يُمرَّر قيد الإرسال فقط
    /// (قيد الاستلام يُولَّد لاحقاً عند موافقة الصندوق المستلم).
    /// </summary>
    public static CashBoxTransfer Create(
        string transferNumber,
        int fromCashBoxId,
        int toCashBoxId,
        int transitAccountId,
        string currency,
        decimal amount,
        DateTime sendDate,
        DateTime expectedReceiveDate,
        int sendJournalEntryId,
        string? description = null,
        string? referenceNumber = null)
    {
        if (string.IsNullOrWhiteSpace(transferNumber)) throw new DomainException("رقم المناقلة مطلوب");
        if (fromCashBoxId <= 0 || toCashBoxId <= 0) throw new DomainException("الصناديق مطلوبة");
        if (fromCashBoxId == toCashBoxId) throw new DomainException("لا يمكن المناقلة بين صندوق ونفسه");
        if (transitAccountId <= 0) throw new DomainException("الحساب الوسيط مطلوب");
        if (string.IsNullOrWhiteSpace(currency)) throw new DomainException("العملة مطلوبة");
        if (amount <= 0) throw new DomainException("المبلغ يجب أن يكون موجباً");
        if (expectedReceiveDate.Date < sendDate.Date) throw new DomainException("تاريخ الاستلام المتوقَّع لا يمكن أن يسبق تاريخ الإرسال");
        if (sendJournalEntryId <= 0) throw new DomainException("قيد الإرسال مطلوب");

        return new CashBoxTransfer
        {
            TransferNumber = transferNumber.Trim(),
            FromCashBoxId = fromCashBoxId,
            ToCashBoxId = toCashBoxId,
            TransitAccountId = transitAccountId,
            Currency = currency.Trim().ToUpperInvariant(),
            Amount = amount,
            SendDate = sendDate,
            ReceiveDate = expectedReceiveDate,
            SendJournalEntryId = sendJournalEntryId,
            ReceiveJournalEntryId = null,
            Status = CashBoxTransferStatus.PendingReceive,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim(),
        };
    }

    /// <summary>
    /// تأكيد الاستلام: يربط قيد الاستلام المتولَّد ويسجِّل المعتمِد والوقت.
    /// </summary>
    public void MarkReceived(int receiveJournalEntryId, DateTime actualReceiveDate, string userId, string? notes)
    {
        if (Status != CashBoxTransferStatus.PendingReceive)
            throw new DomainException($"لا يمكن تأكيد استلام مناقلة حالتها '{Status}'.");
        if (receiveJournalEntryId <= 0) throw new DomainException("قيد الاستلام غير صالح");
        if (actualReceiveDate.Date < SendDate.Date)
            throw new DomainException("تاريخ الاستلام الفعلي لا يمكن أن يسبق تاريخ الإرسال");
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("يجب تحديد المستخدم المعتمِد للاستلام");

        ReceiveJournalEntryId = receiveJournalEntryId;
        ReceiveDate = actualReceiveDate;
        Status = CashBoxTransferStatus.Received;
        ReceivedByUserId = userId.Trim();
        ReceivedAt = DateTime.UtcNow;
        ReceiveNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    /// <summary>
    /// إلغاء المناقلة قبل الاستلام: يربط قيد العكس ويسجِّل سبب الإلغاء والمستخدم.
    /// </summary>
    public void Cancel(int reversalJournalEntryId, string userId, string? reason)
    {
        if (Status != CashBoxTransferStatus.PendingReceive)
            throw new DomainException($"لا يمكن إلغاء مناقلة حالتها '{Status}' — يُلغى فقط من حالة 'بانتظار الاستلام'.");
        if (reversalJournalEntryId <= 0) throw new DomainException("قيد عكس الإرسال غير صالح");
        if (string.IsNullOrWhiteSpace(userId))
            throw new DomainException("يجب تحديد المستخدم الذي ألغى المناقلة");

        ReversalJournalEntryId = reversalJournalEntryId;
        Status = CashBoxTransferStatus.Cancelled;
        CancelledByUserId = userId.Trim();
        CancelledAt = DateTime.UtcNow;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>
    /// تعديل بيانات مناقلة بانتظار الاستلام (المبلغ/التاريخ/الحساب الوسيط/البيان).
    /// لا يُسمح بتغيير الصندوقَين أو العملة (لأنه يقتضي إلغاء المناقلة وإنشاء جديدة).
    /// يُمرَّر معرّف قيد الإرسال الجديد بعد إعادة توليده عبر طبقة التطبيق.
    /// </summary>
    public void UpdatePending(
        int newSendJournalEntryId,
        decimal amount,
        DateTime sendDate,
        int transitAccountId,
        string? description,
        string? referenceNumber)
    {
        if (Status != CashBoxTransferStatus.PendingReceive)
            throw new DomainException($"لا يمكن تعديل مناقلة حالتها '{Status}' — التعديل متاح فقط 'بانتظار الاستلام'.");
        if (newSendJournalEntryId <= 0) throw new DomainException("قيد الإرسال الجديد غير صالح");
        if (amount <= 0) throw new DomainException("المبلغ يجب أن يكون موجباً");
        if (transitAccountId <= 0) throw new DomainException("الحساب الوسيط مطلوب");

        SendJournalEntryId = newSendJournalEntryId;
        Amount = amount;
        SendDate = sendDate;
        TransitAccountId = transitAccountId;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim();
    }

    /// <summary>
    /// التراجع عن الاستلام: يُلغي تأكيد الاستلام ويُعيد المناقلة إلى حالة
    /// "بانتظار الاستلام" بعد عكس قيد الاستلام محاسبياً. يستخدم عندما يحتاج
    /// الصندوق المُرسِل تعديل المناقلة بعد أن وافق المستلم عليها.
    /// </summary>
    public void Unreceive()
    {
        if (Status != CashBoxTransferStatus.Received)
            throw new DomainException($"لا يمكن التراجع عن استلام مناقلة حالتها '{Status}'.");
        ReceiveJournalEntryId = null;
        Status = CashBoxTransferStatus.PendingReceive;
        ReceivedByUserId = null;
        ReceivedAt = null;
        ReceiveNotes = null;
    }
}
