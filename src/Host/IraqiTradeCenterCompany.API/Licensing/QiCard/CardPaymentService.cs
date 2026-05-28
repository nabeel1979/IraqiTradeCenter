using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace IraqiTradeCenterCompany.API.Licensing.QiCard;

// ════════════════════════════════════════════════════════════════════════════
// Public DTOs returned by the service
// ════════════════════════════════════════════════════════════════════════════

/// <summary>نتيجة إنشاء جلسة دفع بالبطاقة — تُعاد للـ frontend ليفتح FormUrl.</summary>
public sealed class CreateCardPaymentResult
{
    public required Guid    SessionId { get; init; }
    public required string  FormUrl   { get; init; }
    public required decimal Amount    { get; init; }
    public required string  Currency  { get; init; }
    public required int     Days      { get; init; }
    public required string  Status    { get; init; }
}

/// <summary>حالة جلسة دفع موجودة — يُستعلَم عنها بالـ polling من الـ frontend.</summary>
public sealed class CardPaymentStatusResult
{
    public required Guid     SessionId      { get; init; }
    public required string   Status         { get; init; }   // Created/Pending/Success/Failed/Expired/Error/Canceled
    public required decimal  Amount         { get; init; }
    public required string   Currency       { get; init; }
    public required int      Days           { get; init; }
    public string?           ErrorMessage   { get; init; }
    public int?              ActivationId   { get; init; }    // إذا نجح، Id الـ activation المُولّد
    public DateTime?         CompletedAt    { get; init; }
}

// ════════════════════════════════════════════════════════════════════════════
// Service interface + implementation
// ════════════════════════════════════════════════════════════════════════════

public interface ICardPaymentService
{
    /// <summary>إنشاء جلسة دفع جديدة بالبطاقة + استدعاء QiCard للحصول على FormUrl.</summary>
    Task<Result<CreateCardPaymentResult>> CreateAsync(int days, string? userId, CancellationToken ct);

    /// <summary>قراءة حالة جلسة دفع (للـ polling).</summary>
    Task<Result<CardPaymentStatusResult>> GetStatusAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// معالجة webhook قادم من QiCard — يُحدّث حالة الجلسة، وإن كانت ناجحة:
    /// يُولّد شفرة ترخيص ويُطبّقها (idempotent — يُهمل التكرار).
    /// </summary>
    Task<Result> HandleWebhookAsync(QiWebhookPayload payload, string rawBody, CancellationToken ct);
}

public sealed class CardPaymentService : ICardPaymentService
{
    private readonly IConfiguration       _cfg;
    private readonly ILicenseService      _license;
    private readonly IQiCardClient        _qicard;
    private readonly QiCardOptions        _opts;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<CardPaymentService> _log;

    public CardPaymentService(
        IConfiguration cfg,
        ILicenseService license,
        IQiCardClient qicard,
        IOptions<QiCardOptions> opts,
        IHttpContextAccessor http,
        ILogger<CardPaymentService> log)
    {
        _cfg     = cfg;
        _license = license;
        _qicard  = qicard;
        _opts    = opts.Value;
        _http    = http;
        _log     = log;
    }

    // ────────────────────────────────────────────────────────────────────
    // 1) CreateAsync
    // ────────────────────────────────────────────────────────────────────

    public async Task<Result<CreateCardPaymentResult>> CreateAsync(int days, string? userId, CancellationToken ct)
    {
        if (!_opts.Enabled)
            return Result.Failure<CreateCardPaymentResult>(
                "بوّابة QiCard غير مفعَّلة في الإعدادات (QiCard:Enabled). يُرجى التواصل مع مركز التجارة العراقي.");

        if (days <= 0)
            return Result.Failure<CreateCardPaymentResult>("عدد الأيام يجب أن يكون أكبر من صفر.");

        var cfg = await _license.GetConfigAsync(ct);
        var amount   = cfg.PricePerDay * days;
        var currency = string.IsNullOrWhiteSpace(_opts.Currency) ? cfg.Currency : _opts.Currency;
        var sessionId = Guid.NewGuid();

        // ‎1) أنشئ صفّ الجلسة بحالة Created (قبل استدعاء QiCard كي نحجز SessionId).
        await using (var cn = Open())
        await using (var ins = cn.CreateCommand())
        {
            ins.CommandText = @"
INSERT INTO [licensing].[CardPayments]
    ([SessionId],[Amount],[Currency],[Days],[Status],[CreatedAt],[CreatedBy])
VALUES (@s, @a, @c, @d, @st, SYSUTCDATETIME(), @u);";
            ins.Parameters.AddWithValue("@s",  sessionId);
            ins.Parameters.AddWithValue("@a",  amount);
            ins.Parameters.AddWithValue("@c",  currency);
            ins.Parameters.AddWithValue("@d",  days);
            ins.Parameters.AddWithValue("@st", CardPaymentStatus.Created);
            ins.Parameters.AddWithValue("@u",  (object?)userId ?? DBNull.Value);
            await ins.ExecuteNonQueryAsync(ct);
        }

        // ‎2) في وضع MOCK: لا نتصل بـ QiCard، نُعيد URL محلّي يقدّم صفحة محاكاة.
        if (_opts.IsMockMode)
        {
            var mockUrl = BuildMockFormUrl(sessionId);
            await using (var cn = Open())
            await using (var upd = cn.CreateCommand())
            {
                upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [QiCardPaymentId] = @pid,
       [FormUrl]         = @url,
       [Status]          = @st,
       [QiCardStatus]    = N'MOCK'
 WHERE [SessionId] = @sid;";
                upd.Parameters.AddWithValue("@pid", sessionId.ToString("N"));
                upd.Parameters.AddWithValue("@url", mockUrl);
                upd.Parameters.AddWithValue("@st",  CardPaymentStatus.Pending);
                upd.Parameters.AddWithValue("@sid", sessionId);
                await upd.ExecuteNonQueryAsync(ct);
            }

            _log.LogInformation("CardPayment {Sid} created in MOCK mode → {Url}", sessionId, mockUrl);
            return Result.Success(new CreateCardPaymentResult
            {
                SessionId = sessionId,
                FormUrl   = mockUrl,
                Amount    = amount,
                Currency  = currency,
                Days      = days,
                Status    = CardPaymentStatus.Pending,
            });
        }

        // ‎3) وضع Live: استدعِ QiCard
        var req = new QiCreatePaymentRequest
        {
            RequestId        = sessionId.ToString("D"),
            Amount           = amount,
            Currency         = currency,
            Locale           = _opts.Locale,
            TerminalId       = _opts.TerminalId,
            Description      = $"شراء ترخيص نظام مركز التجارة العراقي - {days} يوم",
            FinishPaymentUrl = WithSessionId(_opts.FinishPaymentUrl, sessionId),
            NotificationUrl  = _opts.NotificationUrl,
            AdditionalInfo   = new Dictionary<string, string>
            {
                ["companyKey"] = cfg.CompanyKey,
                ["days"]       = days.ToString(),
                ["sessionId"]  = sessionId.ToString("D"),
            },
        };

        QiCreatePaymentResponse resp;
        try   { resp = await _qicard.CreatePaymentAsync(req, ct); }
        catch (Exception ex)
        {
            _log.LogError(ex, "QiCard CreatePayment threw for session {Sid}", sessionId);
            await MarkErrorAsync(sessionId, "EXCEPTION", ex.Message, ct);
            return Result.Failure<CreateCardPaymentResult>(
                "فشل الاتصال ببوّابة QiCard. حاول لاحقاً أو استخدم المحفظة.");
        }

        if (!resp.Success || string.IsNullOrWhiteSpace(resp.FormUrl))
        {
            await MarkErrorAsync(sessionId, resp.ErrorCode, resp.ErrorMessage, ct);
            var msg = resp.ErrorMessage ?? "تعذّر إنشاء طلب الدفع لدى QiCard.";
            return Result.Failure<CreateCardPaymentResult>(msg);
        }

        // ‎4) حدِّث الصفّ بـ PaymentId + FormUrl
        await using (var cn = Open())
        await using (var upd = cn.CreateCommand())
        {
            upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [QiCardPaymentId] = @pid,
       [FormUrl]         = @url,
       [Status]          = @st,
       [QiCardStatus]    = @qst
 WHERE [SessionId] = @sid;";
            upd.Parameters.AddWithValue("@pid", (object?)resp.PaymentId ?? DBNull.Value);
            upd.Parameters.AddWithValue("@url", resp.FormUrl!);
            upd.Parameters.AddWithValue("@st",  CardPaymentStatus.Pending);
            upd.Parameters.AddWithValue("@qst", (object?)resp.Status ?? DBNull.Value);
            upd.Parameters.AddWithValue("@sid", sessionId);
            await upd.ExecuteNonQueryAsync(ct);
        }

        return Result.Success(new CreateCardPaymentResult
        {
            SessionId = sessionId,
            FormUrl   = resp.FormUrl!,
            Amount    = amount,
            Currency  = currency,
            Days      = days,
            Status    = CardPaymentStatus.Pending,
        });
    }

    /// <summary>
    /// يبني URL لصفحة الـ mock الموجودة على نفس الـ Backend. يُفضّل أن يكون
    /// مطلقاً (مع scheme + host) كي يفتح في تبويب جديد بشكل صحيح.
    /// نأخذ المضيف من <see cref="QiCardOptions.NotificationUrl"/> (لأنه مضبوط
    /// على عنوان الـ Backend العام)، وإلّا نستخدم مسار نسبي.
    /// </summary>
    private string BuildMockFormUrl(Guid sessionId)
    {
        var basePath = $"/api/license/qicard/mock/{sessionId:D}";

        // ‎أولوية 1: استنبط الـ host من الطلب الحالي (الـ Backend نفسه).
        var req = _http.HttpContext?.Request;
        if (req != null && req.Host.HasValue)
            return $"{req.Scheme}://{req.Host.Value}{basePath}";

        // ‎أولوية 2: استخرج الـ host من NotificationUrl إن كان مضبوطاً.
        if (!string.IsNullOrWhiteSpace(_opts.NotificationUrl))
        {
            try
            {
                var u = new Uri(_opts.NotificationUrl);
                return $"{u.Scheme}://{u.Authority}{basePath}";
            }
            catch { /* ignore — fallback below */ }
        }

        // ‎fallback: مسار نسبي (يعمل لو الفرونتاند والباكاند نفس الـ origin).
        return basePath;
    }

    // ────────────────────────────────────────────────────────────────────
    // 2) GetStatusAsync
    // ────────────────────────────────────────────────────────────────────

    public async Task<Result<CardPaymentStatusResult>> GetStatusAsync(Guid sessionId, CancellationToken ct)
    {
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT TOP 1 [Status],[Amount],[Currency],[Days],[ErrorMessage],[ActivationId],[CompletedAt]
FROM [licensing].[CardPayments] WHERE [SessionId] = @s;";
        cmd.Parameters.AddWithValue("@s", sessionId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            return Result.Failure<CardPaymentStatusResult>("جلسة الدفع غير موجودة.");

        return Result.Success(new CardPaymentStatusResult
        {
            SessionId    = sessionId,
            Status       = r.GetString(0),
            Amount       = r.GetDecimal(1),
            Currency     = r.GetString(2),
            Days         = r.GetInt32(3),
            ErrorMessage = r.IsDBNull(4) ? null : r.GetString(4),
            ActivationId = r.IsDBNull(5) ? null : r.GetInt32(5),
            CompletedAt  = r.IsDBNull(6) ? null : DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc),
        });
    }

    // ────────────────────────────────────────────────────────────────────
    // 3) HandleWebhookAsync — idempotent
    // ────────────────────────────────────────────────────────────────────

    public async Task<Result> HandleWebhookAsync(QiWebhookPayload payload, string rawBody, CancellationToken ct)
    {
        var paymentId = payload.GetPaymentId();
        var requestId = payload.GetRequestId();

        if (string.IsNullOrWhiteSpace(paymentId) && string.IsNullOrWhiteSpace(requestId))
            return Result.Failure("الـ payload لا يحوي paymentId ولا requestId.");

        // ‎ابحث عن الجلسة — أولاً بـ QiCardPaymentId، وإلّا بـ requestId (= SessionId).
        Guid?   sessionId    = null;
        string? currentStatus = null;
        int     days          = 0;
        await using (var cn = Open())
        await using (var find = cn.CreateCommand())
        {
            if (!string.IsNullOrWhiteSpace(paymentId))
            {
                find.CommandText = @"
SELECT TOP 1 [SessionId],[Status],[Days]
FROM [licensing].[CardPayments] WHERE [QiCardPaymentId] = @p;";
                find.Parameters.AddWithValue("@p", paymentId);
            }
            else
            {
                if (!Guid.TryParse(requestId, out var sid))
                    return Result.Failure("requestId لا يُمكن تحويله إلى GUID صالح.");
                find.CommandText = @"
SELECT TOP 1 [SessionId],[Status],[Days]
FROM [licensing].[CardPayments] WHERE [SessionId] = @s;";
                find.Parameters.AddWithValue("@s", sid);
            }
            await using var r = await find.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                sessionId     = r.GetGuid(0);
                currentStatus = r.GetString(1);
                days          = r.GetInt32(2);
            }
        }
        if (sessionId == null)
            return Result.Failure("لم يُعثر على جلسة دفع تطابق هذا الـ paymentId/requestId.");

        // ‎خزّن الـ raw payload للتدقيق (دائماً، حتى لو كانت الحالة نهائية).
        var normalized = QiCardStatusMapper.Normalize(payload.Status);
        await StoreRawWebhookAsync(sessionId.Value, rawBody, payload.Status, payload.ErrorCode, payload.ErrorMessage, ct);

        // ‎Idempotent: لو الحالة الحالية نهائية، لا نُحدّث ولا نُكرّر التفعيل.
        if (currentStatus != null && QiCardStatusMapper.IsTerminal(currentStatus))
        {
            _log.LogInformation("QiCard webhook for {Sid} ignored — already terminal ({Cur}).",
                sessionId, currentStatus);
            return Result.Success();
        }

        // ‎حدِّث الحالة المنطقية.
        await UpdateStatusAsync(sessionId.Value, normalized, ct);

        // ‎نجاح؟ ولِّد شفرة وطبّقها.
        if (normalized == CardPaymentStatus.Success)
        {
            try
            {
                var code = await _license.GenerateAsync(days, ct);
                var apply = await _license.ApplyCodeAsync(code, "Card", userId: null, ct);
                if (!apply.IsSuccess)
                {
                    _log.LogError("Apply code failed after successful card payment {Sid}: {Errors}",
                        sessionId, string.Join("; ", apply.Errors));
                    await SetErrorAsync(sessionId.Value, "APPLY_FAILED",
                        string.Join("; ", apply.Errors), ct);
                    return Result.Failure("الدفع نجح لكن فشل تفعيل الترخيص — تواصَل مع الدعم.");
                }
                await SetActivatedAsync(sessionId.Value, apply.Value!.Id, ct);
                LicenseEnforcementMiddleware.InvalidateCache();
                _log.LogInformation("Card payment {Sid} activated → activation #{AId} ({Days} days).",
                    sessionId, apply.Value!.Id, days);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Exception during license activation after card payment {Sid}", sessionId);
                await SetErrorAsync(sessionId.Value, "EXCEPTION", ex.Message, ct);
                return Result.Failure("خطأ داخلي عند تفعيل الترخيص بعد الدفع — تواصَل مع الدعم.");
            }
        }

        return Result.Success();
    }

    // ────────────────────────────────────────────────────────────────────
    // Internal helpers
    // ────────────────────────────────────────────────────────────────────

    private SqlConnection Open()
    {
        var cs = _cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection");
        var cn = new SqlConnection(cs);
        cn.Open();
        return cn;
    }

    private async Task UpdateStatusAsync(Guid sessionId, string status, CancellationToken ct)
    {
        await using var cn = Open();
        await using var upd = cn.CreateCommand();
        upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [Status] = @st
 WHERE [SessionId] = @s;";
        upd.Parameters.AddWithValue("@st", status);
        upd.Parameters.AddWithValue("@s",  sessionId);
        await upd.ExecuteNonQueryAsync(ct);
    }

    private async Task StoreRawWebhookAsync(
        Guid sessionId, string rawBody, string? qiStatus, string? errorCode, string? errorMessage, CancellationToken ct)
    {
        await using var cn = Open();
        await using var upd = cn.CreateCommand();
        upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [WebhookRaw]   = @raw,
       [QiCardStatus] = COALESCE(@qst, [QiCardStatus]),
       [ErrorCode]    = COALESCE(@ec,  [ErrorCode]),
       [ErrorMessage] = COALESCE(@em,  [ErrorMessage])
 WHERE [SessionId] = @s;";
        upd.Parameters.AddWithValue("@raw", (object?)rawBody ?? DBNull.Value);
        upd.Parameters.AddWithValue("@qst", (object?)qiStatus ?? DBNull.Value);
        upd.Parameters.AddWithValue("@ec",  (object?)errorCode ?? DBNull.Value);
        upd.Parameters.AddWithValue("@em",  (object?)errorMessage ?? DBNull.Value);
        upd.Parameters.AddWithValue("@s",   sessionId);
        await upd.ExecuteNonQueryAsync(ct);
    }

    private async Task MarkErrorAsync(Guid sessionId, string? errorCode, string? errorMessage, CancellationToken ct)
    {
        await using var cn = Open();
        await using var upd = cn.CreateCommand();
        upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [Status]       = @st,
       [ErrorCode]    = @ec,
       [ErrorMessage] = @em,
       [CompletedAt]  = SYSUTCDATETIME()
 WHERE [SessionId] = @s;";
        upd.Parameters.AddWithValue("@st", CardPaymentStatus.Error);
        upd.Parameters.AddWithValue("@ec", (object?)errorCode ?? DBNull.Value);
        upd.Parameters.AddWithValue("@em", (object?)errorMessage ?? DBNull.Value);
        upd.Parameters.AddWithValue("@s",  sessionId);
        await upd.ExecuteNonQueryAsync(ct);
    }

    private async Task SetErrorAsync(Guid sessionId, string code, string msg, CancellationToken ct)
    {
        await using var cn = Open();
        await using var upd = cn.CreateCommand();
        upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [Status]       = @st,
       [ErrorCode]    = @ec,
       [ErrorMessage] = @em,
       [CompletedAt]  = SYSUTCDATETIME()
 WHERE [SessionId] = @s;";
        upd.Parameters.AddWithValue("@st", CardPaymentStatus.Error);
        upd.Parameters.AddWithValue("@ec", code);
        upd.Parameters.AddWithValue("@em", msg);
        upd.Parameters.AddWithValue("@s",  sessionId);
        await upd.ExecuteNonQueryAsync(ct);
    }

    private async Task SetActivatedAsync(Guid sessionId, int activationId, CancellationToken ct)
    {
        await using var cn = Open();
        await using var upd = cn.CreateCommand();
        upd.CommandText = @"
UPDATE [licensing].[CardPayments]
   SET [ActivationId] = @a,
       [CompletedAt]  = SYSUTCDATETIME()
 WHERE [SessionId] = @s;";
        upd.Parameters.AddWithValue("@a", activationId);
        upd.Parameters.AddWithValue("@s", sessionId);
        await upd.ExecuteNonQueryAsync(ct);
    }

    private static string WithSessionId(string url, Guid sessionId)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        var sep = url.Contains('?') ? '&' : '?';
        return $"{url}{sep}sessionId={sessionId:D}";
    }
}
