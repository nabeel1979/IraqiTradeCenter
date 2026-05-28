using System.Text.Json.Serialization;

namespace IraqiTradeCenterCompany.API.Licensing.QiCard;

// ════════════════════════════════════════════════════════════════════════════
// Request models — ما نُرسله إلى QiCard
// ════════════════════════════════════════════════════════════════════════════

public sealed class QiCustomerInfo
{
    [JsonPropertyName("first_name")]    public string? FirstName    { get; set; }
    [JsonPropertyName("last_name")]     public string? LastName     { get; set; }
    [JsonPropertyName("customer_name")] public string? CustomerName { get; set; }
    [JsonPropertyName("email")]         public string? Email        { get; set; }
    [JsonPropertyName("phone")]         public string? Phone        { get; set; }
}

public sealed class QiCreatePaymentRequest
{
    [JsonPropertyName("request_id")]          public string  RequestId         { get; set; } = "";
    [JsonPropertyName("amount")]              public decimal Amount            { get; set; }
    [JsonPropertyName("currency")]            public string  Currency          { get; set; } = "IQD";
    [JsonPropertyName("locale")]              public string  Locale            { get; set; } = "ar_IQ";
    [JsonPropertyName("description")]         public string? Description       { get; set; }
    [JsonPropertyName("terminal_id")]         public string  TerminalId        { get; set; } = "";
    [JsonPropertyName("finish_payment_url")]  public string  FinishPaymentUrl  { get; set; } = "";
    [JsonPropertyName("notification_url")]    public string  NotificationUrl   { get; set; } = "";
    [JsonPropertyName("customer_info")]       public QiCustomerInfo? CustomerInfo { get; set; }
    [JsonPropertyName("additional_info")]     public Dictionary<string, string>? AdditionalInfo { get; set; }
}

// ════════════════════════════════════════════════════════════════════════════
// Response models — ما تُرجعه QiCard
// ════════════════════════════════════════════════════════════════════════════

public sealed class QiCreatePaymentResponse
{
    [JsonPropertyName("success")]       public bool    Success      { get; set; }
    [JsonPropertyName("paymentId")]     public string? PaymentId    { get; set; }
    [JsonPropertyName("formUrl")]       public string? FormUrl      { get; set; }
    [JsonPropertyName("status")]        public string? Status       { get; set; }
    [JsonPropertyName("paymentType")]   public string? PaymentType  { get; set; }
    [JsonPropertyName("errorCode")]     public string? ErrorCode    { get; set; }
    [JsonPropertyName("errorMessage")]  public string? ErrorMessage { get; set; }
}

public sealed class QiPaymentStatusResponse
{
    [JsonPropertyName("paymentId")]    public string? PaymentId    { get; set; }
    [JsonPropertyName("status")]       public string? Status       { get; set; }
    [JsonPropertyName("amount")]       public decimal Amount       { get; set; }
    [JsonPropertyName("currency")]     public string? Currency     { get; set; }
    [JsonPropertyName("paymentType")]  public string? PaymentType  { get; set; }
    [JsonPropertyName("errorCode")]    public string? ErrorCode    { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }
}

/// <summary>
/// شكل جسم الـ webhook الذي ترسله QiCard. أسماء الحقول مرنة — نقبل أكثر من شكل
/// ونلتقط ما نقدر عليه.
/// </summary>
public sealed class QiWebhookPayload
{
    [JsonPropertyName("paymentId")]    public string? PaymentId    { get; set; }
    [JsonPropertyName("payment_id")]   public string? PaymentIdAlt { get; set; }
    [JsonPropertyName("requestId")]    public string? RequestId    { get; set; }
    [JsonPropertyName("request_id")]   public string? RequestIdAlt { get; set; }
    [JsonPropertyName("status")]       public string? Status       { get; set; }
    [JsonPropertyName("amount")]       public decimal? Amount      { get; set; }
    [JsonPropertyName("currency")]     public string? Currency     { get; set; }
    [JsonPropertyName("paymentType")]  public string? PaymentType  { get; set; }
    [JsonPropertyName("errorCode")]    public string? ErrorCode    { get; set; }
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; set; }

    /// <summary>إرجاع PaymentId من أي مفتاح متاح.</summary>
    public string? GetPaymentId() => !string.IsNullOrWhiteSpace(PaymentId) ? PaymentId : PaymentIdAlt;
    /// <summary>إرجاع RequestId من أي مفتاح متاح.</summary>
    public string? GetRequestId() => !string.IsNullOrWhiteSpace(RequestId) ? RequestId : RequestIdAlt;
}

// ════════════════════════════════════════════════════════════════════════════
// Status normalization
// ════════════════════════════════════════════════════════════════════════════

/// <summary>
/// حالات الدفع المنطقية في نظامنا. <see cref="QiCardStatusMapper"/> يحوّل القيم
/// الواردة من QiCard إلى هذه الحالات.
/// </summary>
public static class CardPaymentStatus
{
    public const string Created    = "Created";    // أُنشئت الجلسة لكن المستخدم لم يفتح الصفحة بعد
    public const string Pending    = "Pending";    // المستخدم في صفحة QiCard لكن لم يُكمل الدفع
    public const string Success    = "Success";    // الدفع نجح وتمّ تفعيل الترخيص
    public const string Failed     = "Failed";     // الدفع رُفض/فشل
    public const string Expired    = "Expired";    // الجلسة انتهت قبل إكمال الدفع
    public const string Error      = "Error";      // خطأ تقني
    public const string Canceled   = "Canceled";   // المستخدم ألغى الدفع
}

public static class QiCardStatusMapper
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return CardPaymentStatus.Pending;
        return raw.Trim().ToUpperInvariant() switch
        {
            "CREATED"                  => CardPaymentStatus.Created,
            "FORM_SHOWED"              => CardPaymentStatus.Pending,
            "AUTHENTICATION_REQUIRED"  => CardPaymentStatus.Pending,
            "AUTHENTICATION_STARTED"   => CardPaymentStatus.Pending,
            "AUTHENTICATION_FAILED"    => CardPaymentStatus.Failed,
            "AUTHENTICATED"            => CardPaymentStatus.Pending,
            "INITIALIZED"              => CardPaymentStatus.Pending,
            "STARTED"                  => CardPaymentStatus.Pending,
            "SUCCESS"                  => CardPaymentStatus.Success,
            "APPROVED"                 => CardPaymentStatus.Success,
            "FAILED"                   => CardPaymentStatus.Failed,
            "REJECTED"                 => CardPaymentStatus.Failed,
            "ERROR"                    => CardPaymentStatus.Error,
            "EXPIRED"                  => CardPaymentStatus.Expired,
            "CANCELED"                 => CardPaymentStatus.Canceled,
            "CANCELLED"                => CardPaymentStatus.Canceled,
            _                          => CardPaymentStatus.Pending,
        };
    }

    /// <summary>هل الحالة نهائية (لا تتغيّر بعدها)؟ Success/Failed/Expired/Error/Canceled.</summary>
    public static bool IsTerminal(string status) =>
        status is CardPaymentStatus.Success
               or CardPaymentStatus.Failed
               or CardPaymentStatus.Expired
               or CardPaymentStatus.Error
               or CardPaymentStatus.Canceled;
}
