namespace IraqiTradeCenterCompany.API.Licensing.QiCard;

/// <summary>
/// إعدادات بوّابة دفع QiCard — تُربط من قسم <c>"QiCard"</c> في الـ configuration.
///
/// مثال (في appsettings.Local.json):
/// <code>
/// "QiCard": {
///   "Enabled":           true,
///   "BaseUrl":           "https://uat-sandbox-3ds-api.qi.iq/api/v1",
///   "Username":          "your-merchant-username",
///   "Password":          "your-merchant-password",
///   "TerminalId":        "your-terminal-id",
///   "Currency":          "IQD",
///   "FinishPaymentUrl":  "https://iraqitradecenter_company.gcc.iq/payment/finish",
///   "NotificationUrl":   "https://api_iraqitradecenter_company.gcc.iq/api/license/qicard/webhook",
///   "WebhookSecret":     "shared-secret-for-webhook-auth",
///   "CreatePaymentPath": "/payment",
///   "TimeoutSeconds":    30
/// }
/// </code>
///
/// الحقول <c>Username</c> و<c>Password</c> و<c>TerminalId</c> تأتي من QiCard عند
/// تسجيل الـ Merchant. الـ <c>WebhookSecret</c> اختياري — لو ضُبط، نتحقّق من ترويسة
/// <c>X-Webhook-Secret</c> في طلبات الـ webhook (طبقة حماية إضافية).
/// </summary>
public sealed class QiCardOptions
{
    public const string SectionName = "QiCard";

    /// <summary>تفعيل/تعطيل البوّابة. لو false → نُعيد رسالة "قيد التكامل" كما كان.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// وضع التشغيل:
    ///   • <c>"Live"</c> (افتراضي): اتصال فعلي بـ QiCard — يتطلّب credentials صحيحة.
    ///   • <c>"Mock"</c>: محاكاة كاملة لـ QiCard من داخل النظام بدون اتصال خارجي.
    ///     مفيد للعروض/الاختبار قبل وصول credentials. صفحة الدفع تُقدَّم من
    ///     <c>GET /api/license/qicard/mock/{sessionId}</c> ويُحاكي الـ webhook
    ///     عند ضغط زرّ "دفع" أو "رفض" على تلك الصفحة.
    /// </summary>
    public string Mode { get; set; } = "Live";

    public bool IsMockMode => string.Equals(Mode, "Mock", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// عنوان الـ API الأساسي. Sandbox: <c>https://uat-sandbox-3ds-api.qi.iq/api/v1</c>.
    /// Production يأتي من QiCard عند اعتماد الحساب.
    /// </summary>
    public string BaseUrl { get; set; } = "https://uat-sandbox-3ds-api.qi.iq/api/v1";

    public string Username   { get; set; } = "";
    public string Password   { get; set; } = "";
    public string TerminalId { get; set; } = "";

    /// <summary>العملة المرسلة إلى QiCard. الافتراضي IQD.</summary>
    public string Currency { get; set; } = "IQD";

    /// <summary>
    /// URL التي تُعيد QiCard المستخدم إليها بعد الدفع (Sync). نمرّر فيها
    /// <c>sessionId</c> كـ query parameter لاحقاً (يضاف بـ <c>?sessionId=...</c>).
    /// </summary>
    public string FinishPaymentUrl { get; set; } = "";

    /// <summary>
    /// URL التي تتصل بها QiCard بشكل غير متزامن (webhook) لإبلاغنا بنتيجة الدفع.
    /// يجب أن تكون مفتوحة من الإنترنت بدون مصادقة JWT (نحميها بـ WebhookSecret).
    /// </summary>
    public string NotificationUrl { get; set; } = "";

    /// <summary>
    /// سرٌّ مشترك (اختياري) نتوقّع وصوله في ترويسة <c>X-Webhook-Secret</c> في طلبات
    /// الـ webhook. لو فارغ → نقبل بدون تحقّق (غير مستحسن في الإنتاج).
    /// </summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>المسار النسبي لإنشاء طلب دفع جديد. افتراضياً <c>/payment</c>.</summary>
    public string CreatePaymentPath { get; set; } = "/payment";

    /// <summary>المسار النسبي لاستعلام حالة دفعة. <c>/payment/{paymentId}</c>.</summary>
    public string GetPaymentPath { get; set; } = "/payment/{paymentId}";

    /// <summary>مهلة طلبات HTTP بالثواني.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>اللغة المُعرَّضة في صفحة الدفع. <c>ar_IQ</c> أو <c>en_US</c>.</summary>
    public string Locale { get; set; } = "ar_IQ";
}
