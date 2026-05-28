using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace IraqiTradeCenterCompany.API.Licensing.QiCard;

/// <summary>
/// عميل HTTP لبوّابة دفع QiCard. يُستخدم HttpClientFactory مع
/// <see cref="QiCardOptions"/> المُحقَن من الـ configuration.
///
/// خطوات الدفع القياسية:
///   1) <see cref="CreatePaymentAsync"/>: يُرسل طلب إنشاء دفعة.
///   2) المتصفّح يُعاد توجيهه إلى <c>FormUrl</c> الذي يُعاد في الاستجابة.
///   3) QiCard ترسل ثلاث إشعارات:
///      • Sync: redirect إلى <c>FinishPaymentUrl</c> + query params (success/failure).
///      • Async: HTTP POST إلى <c>NotificationUrl</c> (webhook) — مصدر الحقيقة.
///   4) (اختياري) <see cref="GetPaymentAsync"/>: للاستعلام النشط عن الحالة.
/// </summary>
public interface IQiCardClient
{
    Task<QiCreatePaymentResponse> CreatePaymentAsync(QiCreatePaymentRequest req, CancellationToken ct);
    Task<QiPaymentStatusResponse?> GetPaymentAsync(string paymentId, CancellationToken ct);
}

public sealed class QiCardClient : IQiCardClient
{
    public const string HttpClientName = "QiCard";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _factory;
    private readonly QiCardOptions     _opts;
    private readonly ILogger<QiCardClient> _log;

    public QiCardClient(IHttpClientFactory factory, IOptions<QiCardOptions> opts, ILogger<QiCardClient> log)
    {
        _factory = factory;
        _opts    = opts.Value;
        _log     = log;
    }

    public async Task<QiCreatePaymentResponse> CreatePaymentAsync(QiCreatePaymentRequest req, CancellationToken ct)
    {
        EnsureConfigured();

        using var http = NewClient();
        using var body = JsonContent(req);
        var path = _opts.CreatePaymentPath;

        _log.LogInformation("QiCard CreatePayment → {Path} request_id={Rid} amount={Amount} {Cur}",
            path, req.RequestId, req.Amount, req.Currency);

        using var resp = await http.PostAsync(path, body, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("QiCard CreatePayment failed status={Status} body={Body}", resp.StatusCode, Trim(raw));
            return new QiCreatePaymentResponse
            {
                Success      = false,
                Status       = "HTTP_ERROR",
                ErrorCode    = ((int)resp.StatusCode).ToString(),
                ErrorMessage = $"QiCard استجاب بـ {(int)resp.StatusCode}",
            };
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<QiCreatePaymentResponse>(raw, JsonOpts);
            if (parsed == null)
                return new QiCreatePaymentResponse { Success = false, ErrorMessage = "استجابة فارغة من QiCard" };

            // ‎بعض الـ APIs لا تُرجع حقل success — نستنتجها من وجود formUrl.
            if (!parsed.Success && !string.IsNullOrWhiteSpace(parsed.FormUrl))
                parsed.Success = true;
            return parsed;
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "QiCard CreatePayment: failed to parse body {Body}", Trim(raw));
            return new QiCreatePaymentResponse
            {
                Success      = false,
                ErrorMessage = "تعذّر تحليل استجابة QiCard",
            };
        }
    }

    public async Task<QiPaymentStatusResponse?> GetPaymentAsync(string paymentId, CancellationToken ct)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(paymentId)) return null;

        using var http = NewClient();
        var path = _opts.GetPaymentPath.Replace("{paymentId}", Uri.EscapeDataString(paymentId), StringComparison.Ordinal);

        try
        {
            using var resp = await http.GetAsync(path, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("QiCard GetPayment {PaymentId} returned {Status}", paymentId, resp.StatusCode);
                return null;
            }
            var raw = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<QiPaymentStatusResponse>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "QiCard GetPayment {PaymentId} threw", paymentId);
            return null;
        }
    }

    private HttpClient NewClient()
    {
        var http = _factory.CreateClient(HttpClientName);
        if (http.BaseAddress == null)
        {
            // ‎في حال لم يُهيَّأ الـ named client (مثلاً في وحدة اختبار) — نضبط هنا.
            http.BaseAddress = new Uri(_opts.BaseUrl.TrimEnd('/') + "/");
            http.Timeout     = TimeSpan.FromSeconds(_opts.TimeoutSeconds);
        }
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_opts.Username}:{_opts.Password}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        http.DefaultRequestHeaders.TryAddWithoutValidation("Terminal-Id", _opts.TerminalId);
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private void EnsureConfigured()
    {
        if (!_opts.Enabled)
            throw new InvalidOperationException("QiCard payment gateway is disabled (QiCard:Enabled = false).");
        if (string.IsNullOrWhiteSpace(_opts.BaseUrl))
            throw new InvalidOperationException("QiCard BaseUrl is not configured.");
        if (string.IsNullOrWhiteSpace(_opts.Username) || string.IsNullOrWhiteSpace(_opts.Password))
            throw new InvalidOperationException("QiCard Username/Password are not configured.");
        if (string.IsNullOrWhiteSpace(_opts.TerminalId))
            throw new InvalidOperationException("QiCard TerminalId is not configured.");
    }

    private static StringContent JsonContent<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOpts);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string Trim(string s) => s.Length > 1024 ? s[..1024] + "…" : s;
}
