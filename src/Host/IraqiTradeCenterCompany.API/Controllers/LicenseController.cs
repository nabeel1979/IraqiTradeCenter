using System.Security.Claims;
using System.Text.Json;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Licensing;
using IraqiTradeCenterCompany.API.Licensing.QiCard;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// واجهات ترخيص النظام:
///   • <c>GET  /api/license/status</c>       — قراءة الحالة (مفتوحة لكل مستخدم مسجَّل)
///   • <c>GET  /api/license/history</c>      — سجل التفعيلات السابقة (Read perm)
///   • <c>POST /api/license/apply</c>        — تطبيق شفرة جديدة (Apply perm)
///   • <c>POST /api/license/buy-with-wallet</c> — شراء من المحفظة فوراً (Apply perm)
///   • <c>POST /api/license/buy-with-card</c>   — إنشاء طلب دفع بالبطاقة (Apply perm)
///   • <c>POST /api/license/generate</c>     — توليد شفرة لاستعمال إداري (Generate perm)
///
/// <c>status</c> لا تتطلّب صلاحية إضافية كي يستطيع المستخدم رؤية حالة الانتهاء حتى لو
/// كانت بقية الـ API محجوبة بسبب انتهاء الترخيص.
/// </summary>
public class LicenseController : BaseApiController
{
    private readonly ILicenseService      _svc;
    private readonly IWalletService       _wallet;
    private readonly ICardPaymentService  _card;
    private readonly QiCardOptions        _qiOpts;
    private readonly ILogger<LicenseController> _log;

    public LicenseController(
        ILicenseService svc,
        IWalletService wallet,
        ICardPaymentService card,
        IOptions<QiCardOptions> qiOpts,
        ILogger<LicenseController> log)
    {
        _svc    = svc;
        _wallet = wallet;
        _card   = card;
        _qiOpts = qiOpts.Value;
        _log    = log;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(new { success = true, data = await _svc.GetStatusAsync(ct) });

    [HttpGet("history")]
    [RequirePermission(PermissionRegistry.System.License.Read)]
    public async Task<IActionResult> History([FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(new { success = true, data = await _svc.GetHistoryAsync(take, ct) });

    [HttpPost("apply")]
    [RequirePermission(PermissionRegistry.System.License.Apply)]
    public async Task<IActionResult> Apply([FromBody] ApplyCodeDto dto, CancellationToken ct)
    {
        var r = await _svc.ApplyCodeAsync(dto.Code ?? "", "Code", CurrentUserId(), ct);
        if (r.IsSuccess) LicenseEnforcementMiddleware.InvalidateCache();
        return HandleResult(r);
    }

    [HttpPost("buy-with-wallet")]
    [RequirePermission(PermissionRegistry.System.License.Apply)]
    public async Task<IActionResult> BuyWithWallet([FromBody] BuyDto dto, CancellationToken ct)
    {
        if (dto.Days <= 0)
            return BadRequest(new { success = false, errors = new[] { "عدد الأيام يجب أن يكون أكبر من صفر." } });

        var cfg = await _svc.GetConfigAsync(ct);
        var cost = cfg.PricePerDay * dto.Days;

        // ‎خصم من المحفظة (يَفشل لو الرصيد غير كافٍ).
        var charge = await _wallet.ChargeAsync(cost, "PayLicense",
            refId: null,
            note:  $"شراء ترخيص {dto.Days} يوم بسعر {cfg.PricePerDay:N3} {cfg.Currency}/يوم",
            userId: CurrentUserId(), ct);
        if (!charge.IsSuccess) return BadRequest(new { success = false, errors = charge.Errors });

        // ‎توليد الشفرة محلياً + تطبيقها مباشرة (دون تكرار).
        var code = await _svc.GenerateAsync(dto.Days, ct);
        var apply = await _svc.ApplyCodeAsync(code, "Wallet", CurrentUserId(), ct);

        if (!apply.IsSuccess)
        {
            // ‎ردّ الأموال للمحفظة لو فشل التطبيق لسبب نادر.
            await _wallet.TopupAsync(cost, "Refund", null,
                "ردّ تلقائي بعد فشل تطبيق شفرة ترخيص اشتُريت من المحفظة",
                CurrentUserId(), ct);
            return BadRequest(new { success = false, errors = apply.Errors });
        }
        LicenseEnforcementMiddleware.InvalidateCache();
        return Ok(new { success = true, data = apply.Value });
    }

    /// <summary>
    /// إنشاء جلسة دفع بالبطاقة عبر بوّابة QiCard. يُعيد <c>formUrl</c> الذي
    /// يفتحه الـ frontend في تبويب جديد ليتمّ المستخدم الدفع، وَ<c>sessionId</c>
    /// الذي يُستعمَل في الـ polling عبر <c>GET /license/qicard/status/{sessionId}</c>.
    ///
    /// لو <c>QiCard:Enabled = false</c> في الإعدادات → نُعيد رسالة "قيد التكامل".
    /// </summary>
    [HttpPost("buy-with-card")]
    [RequirePermission(PermissionRegistry.System.License.Apply)]
    public async Task<IActionResult> BuyWithCard([FromBody] BuyDto dto, CancellationToken ct)
    {
        if (dto.Days <= 0)
            return BadRequest(new { success = false, errors = new[] { "عدد الأيام يجب أن يكون أكبر من صفر." } });

        if (!_qiOpts.Enabled)
        {
            var cfg = await _svc.GetConfigAsync(ct);
            return Ok(new
            {
                success = true,
                data = new
                {
                    method   = "Card",
                    amount   = cfg.PricePerDay * dto.Days,
                    currency = cfg.Currency,
                    days     = dto.Days,
                    status   = "Pending",
                    message  = "بوّابة الدفع بالبطاقة قيد التكامل. يُمكن الشراء حالياً من خلال المحفظة أو إدخال شفرة من مركز التجارة العراقي.",
                }
            });
        }

        var r = await _card.CreateAsync(dto.Days, CurrentUserId(), ct);
        if (!r.IsSuccess)
            return BadRequest(new { success = false, errors = r.Errors });

        var v = r.Value!;
        return Ok(new
        {
            success = true,
            data = new
            {
                method    = "Card",
                sessionId = v.SessionId,
                formUrl   = v.FormUrl,
                amount    = v.Amount,
                currency  = v.Currency,
                days      = v.Days,
                status    = v.Status,
                message   = "تمّ إنشاء طلب الدفع — أكمل عملية الدفع في النافذة المفتوحة.",
            }
        });
    }

    /// <summary>
    /// قراءة حالة جلسة دفع بالبطاقة. يُستخدم بالـ polling من الـ frontend كل بضع ثوانٍ
    /// حتى تصبح الحالة نهائية (Success / Failed / Expired / Error / Canceled).
    /// </summary>
    [HttpGet("qicard/status/{sessionId:guid}")]
    [RequirePermission(PermissionRegistry.System.License.Apply)]
    public async Task<IActionResult> QiCardStatus(Guid sessionId, CancellationToken ct)
    {
        var r = await _card.GetStatusAsync(sessionId, ct);
        if (!r.IsSuccess) return NotFound(new { success = false, errors = r.Errors });
        return Ok(new { success = true, data = r.Value });
    }

    /// <summary>
    /// نقطة استقبال webhook من QiCard — تُستدعى من خوادمهم بشكل غير متزامن بعد
    /// كل تغيّر في حالة الدفع. غير محمية بـ JWT (يستحيل عليهم تمرير JWT)، نحميها
    /// بسرّ مشترك في ترويسة <c>X-Webhook-Secret</c> إن ضُبط في الإعدادات.
    ///
    /// التعامل الـ idempotent: استدعاء webhook متكرّر لنفس الـ paymentId لا يُكرّر التفعيل.
    /// </summary>
    [HttpPost("qicard/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> QiCardWebhook(CancellationToken ct)
    {
        // ‎تحقّق من السرّ المشترك (إن ضُبط).
        if (!string.IsNullOrWhiteSpace(_qiOpts.WebhookSecret))
        {
            var hdr = Request.Headers["X-Webhook-Secret"].ToString();
            if (!string.Equals(hdr, _qiOpts.WebhookSecret, StringComparison.Ordinal))
            {
                _log.LogWarning("QiCard webhook rejected: invalid/missing X-Webhook-Secret header from {Ip}",
                    HttpContext.Connection.RemoteIpAddress);
                return Unauthorized(new { success = false, errors = new[] { "Invalid webhook secret." } });
            }
        }

        // ‎اقرأ الـ body كنصّ أوّلاً (للتدقيق) ثم حلّله.
        Request.EnableBuffering();
        Request.Body.Position = 0;
        using var sr = new StreamReader(Request.Body, leaveOpen: true);
        var raw = await sr.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        QiWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<QiWebhookPayload>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "QiCard webhook: failed to parse JSON body");
            return BadRequest(new { success = false, errors = new[] { "Invalid JSON payload." } });
        }
        if (payload == null)
            return BadRequest(new { success = false, errors = new[] { "Empty payload." } });

        var r = await _card.HandleWebhookAsync(payload, raw, ct);
        // ‎نُرجع 200 دائماً عند عدم وجود خطأ فادح، حتى لا تُكرّر QiCard المحاولة بلا داعٍ
        // ‎في حالات مثل "جلسة غير موجودة" — هذا يُعد خطأ تكوين، ليس خطأ شبكة.
        if (!r.IsSuccess)
        {
            _log.LogWarning("QiCard webhook handled with warning: {Errors}", string.Join("; ", r.Errors));
            return Ok(new { success = false, errors = r.Errors });
        }
        return Ok(new { success = true });
    }

    // ════════════════════════════════════════════════════════════════════
    // MOCK mode endpoints — تُفعَّل فقط حين QiCard:Mode = "Mock"
    // تسمح باختبار التدفّق كاملاً بدون credentials فعلية.
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// صفحة دفع وهميّة تُحاكي شاشة QiCard. تُفتح في تبويب جديد بعد
    /// <c>POST /buy-with-card</c> عندما يكون <c>QiCard:Mode = "Mock"</c>.
    /// تعرض زرّ "ادفع الآن" و "ارفض" — كل ضغطة تستدعي endpoint
    /// <c>/qicard/mock/{sessionId}/complete</c> لمحاكاة الـ webhook.
    /// </summary>
    [HttpGet("qicard/mock/{sessionId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> QiCardMockPage(Guid sessionId, CancellationToken ct)
    {
        if (!_qiOpts.IsMockMode) return NotFound();

        var s = await _card.GetStatusAsync(sessionId, ct);
        if (!s.IsSuccess) return NotFound("جلسة الدفع غير موجودة.");
        var v = s.Value!;

        var amountFmt = v.Amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        var shortId   = sessionId.ToString("N")[..12];
        var html = $$"""
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>QiCard — بوّابة الدفع</title>
  <style>
    *{box-sizing:border-box;margin:0;padding:0}
    body{font-family:'Segoe UI',Tahoma,Arial,sans-serif;min-height:100vh;
         background:linear-gradient(160deg,#0c1445 0%,#1a237e 60%,#283593 100%);
         display:flex;flex-direction:column;align-items:center;justify-content:center;
         padding:16px;color:#1e293b}

    /* ─── top bar ─── */
    .topbar{width:100%;max-width:520px;display:flex;align-items:center;
            justify-content:space-between;margin-bottom:12px;padding:0 4px}
    .topbar-left{display:flex;align-items:center;gap:8px}
    .topbar-brand{color:#fff;font-weight:700;font-size:15px;letter-spacing:.3px}
    .topbar-brand span{opacity:.6;font-weight:400;font-size:12px;margin-right:6px}
    .secure-badge{display:flex;align-items:center;gap:4px;background:rgba(255,255,255,.12);
                  border:1px solid rgba(255,255,255,.2);border-radius:20px;
                  padding:3px 10px;color:#a5f3fc;font-size:11px;font-weight:600}
    .secure-badge svg{width:12px;height:12px;fill:currentColor}
    .mock-badge{background:rgba(251,191,36,.15);border:1px solid rgba(251,191,36,.4);
                border-radius:20px;padding:2px 8px;color:#fbbf24;font-size:10px;font-weight:700;
                letter-spacing:.5px}

    /* ─── card ─── */
    .card{background:#fff;border-radius:20px;max-width:520px;width:100%;
          box-shadow:0 24px 80px rgba(0,0,0,.45);overflow:hidden}

    /* ─── order summary ─── */
    .summary{background:linear-gradient(135deg,#1a237e,#283593);color:#fff;
             padding:20px 24px;display:flex;align-items:center;justify-content:space-between;gap:12px}
    .summary-merchant{display:flex;align-items:center;gap:10px}
    .merchant-icon{width:44px;height:44px;background:rgba(255,255,255,.15);border-radius:10px;
                   display:flex;align-items:center;justify-content:center;font-size:20px}
    .merchant-name{font-weight:700;font-size:14px}
    .merchant-sub{opacity:.7;font-size:12px;margin-top:2px}
    .summary-amount{text-align:left}
    .amount-value{font-size:28px;font-weight:800;line-height:1}
    .amount-label{opacity:.7;font-size:11px;margin-top:3px;text-align:center}

    /* ─── order details strip ─── */
    .details{background:#f8fafc;border-bottom:1px solid #e2e8f0;
             padding:12px 24px;display:flex;gap:0;font-size:12px}
    .detail-item{flex:1;display:flex;flex-direction:column;align-items:center;
                 gap:2px;border-left:1px solid #e2e8f0;padding:0 12px}
    .detail-item:last-child{border-left:0}
    .detail-label{color:#94a3b8;font-size:11px}
    .detail-value{font-weight:600;color:#0f172a;font-size:13px}
    .detail-value.mono{font-family:monospace;font-size:10px;word-break:break-all}

    /* ─── form body ─── */
    .form-body{padding:24px}
    .section-title{font-size:13px;font-weight:700;color:#475569;
                   text-transform:uppercase;letter-spacing:.5px;margin-bottom:14px;
                   display:flex;align-items:center;gap:6px}
    .section-title::after{content:'';flex:1;height:1px;background:#e2e8f0}

    /* card visual */
    .card-visual{background:linear-gradient(135deg,#1a237e,#4527a0);border-radius:12px;
                 padding:18px 20px;margin-bottom:20px;color:#fff;
                 box-shadow:0 8px 24px rgba(26,35,126,.35);position:relative;overflow:hidden}
    .card-visual::before{content:'';position:absolute;top:-30px;right:-30px;
                         width:120px;height:120px;background:rgba(255,255,255,.06);border-radius:50%}
    .card-visual::after{content:'';position:absolute;bottom:-20px;left:20px;
                        width:80px;height:80px;background:rgba(255,255,255,.04);border-radius:50%}
    .cv-top{display:flex;justify-content:space-between;align-items:center;margin-bottom:16px}
    .cv-chip{width:36px;height:26px;background:linear-gradient(135deg,#ffd700,#ffa500);
             border-radius:5px;position:relative;z-index:1}
    .cv-chip::after{content:'';position:absolute;top:50%;left:0;right:0;
                    height:1px;background:rgba(0,0,0,.2);transform:translateY(-50%)}
    .cv-network{display:flex;align-items:center;gap:-4px;position:relative;z-index:1}
    .cv-circle{width:22px;height:22px;border-radius:50%;opacity:.9}
    .cv-circle:first-child{background:#eb001b}
    .cv-circle:last-child{background:#f79e1b;margin-right:-8px}
    .cv-number{font-size:15px;font-weight:600;letter-spacing:3px;font-family:monospace;
               position:relative;z-index:1;margin-bottom:12px;min-height:20px}
    .cv-bottom{display:flex;justify-content:space-between;position:relative;z-index:1}
    .cv-field{display:flex;flex-direction:column;gap:2px}
    .cv-field-label{font-size:9px;opacity:.6;text-transform:uppercase;letter-spacing:.5px}
    .cv-field-value{font-size:12px;font-weight:600;min-height:16px}

    /* inputs */
    .field{margin-bottom:14px}
    .field label{display:block;font-size:12px;font-weight:600;color:#475569;margin-bottom:5px}
    .field label .req{color:#ef4444}
    .input-wrap{position:relative}
    .input-wrap input{width:100%;padding:11px 14px;border:1.5px solid #e2e8f0;border-radius:10px;
                      font-size:14px;color:#0f172a;outline:none;transition:border-color .2s,box-shadow .2s;
                      background:#fff;direction:ltr;text-align:left}
    .input-wrap input::placeholder{color:#cbd5e1;direction:rtl;text-align:right}
    .input-wrap input:focus{border-color:#3b82f6;box-shadow:0 0 0 3px rgba(59,130,246,.1)}
    .input-wrap input.error{border-color:#ef4444}
    .input-icon{position:absolute;top:50%;left:12px;transform:translateY(-50%);
                color:#94a3b8;pointer-events:none;font-size:15px}
    .field-row{display:grid;grid-template-columns:1fr 1fr;gap:12px}
    .err-msg{font-size:11px;color:#ef4444;margin-top:4px;display:none}
    .err-msg.show{display:block}

    /* network icons row */
    .networks{display:flex;gap:8px;margin-bottom:16px}
    .net-icon{height:28px;padding:3px 10px;border:1.5px solid #e2e8f0;border-radius:6px;
              display:flex;align-items:center;justify-content:center;font-size:11px;
              font-weight:700;cursor:default;color:#64748b;background:#f8fafc}
    .net-icon.visa{color:#1a1f71}
    .net-icon.mc{color:#eb001b}
    .net-icon.mada{color:#00923f}
    .net-icon.qi{color:#6d28d9;background:#f5f3ff;border-color:#ddd6fe}

    /* 3DS badge */
    .sec-row{display:flex;align-items:center;gap:6px;font-size:11px;color:#64748b;
             margin-bottom:18px}
    .sec-row svg{color:#10b981;width:14px;height:14px;fill:currentColor;flex-shrink:0}

    /* pay button */
    .btn-pay{width:100%;padding:15px;background:linear-gradient(135deg,#1a237e,#3949ab);
             color:#fff;border:0;border-radius:12px;font-size:16px;font-weight:700;
             cursor:pointer;transition:all .2s;display:flex;align-items:center;
             justify-content:center;gap:8px;letter-spacing:.3px}
    .btn-pay:hover:not(:disabled){background:linear-gradient(135deg,#0d1654,#283593);
                                   transform:translateY(-1px);box-shadow:0 6px 20px rgba(26,35,126,.35)}
    .btn-pay:disabled{opacity:.6;cursor:not-allowed;transform:none}
    .spinner{width:18px;height:18px;border:2px solid rgba(255,255,255,.3);
             border-top-color:#fff;border-radius:50%;animation:spin .7s linear infinite;display:none}
    @keyframes spin{
      to{transform:rotate(360deg)}
    }

    /* secondary actions */
    .sec-actions{display:flex;gap:8px;margin-top:12px}
    .btn-sec{flex:1;padding:11px;background:#f8fafc;border:1.5px solid #e2e8f0;border-radius:10px;
             font-size:13px;font-weight:600;cursor:pointer;transition:all .15s;color:#475569}
    .btn-sec:hover:not(:disabled){background:#f1f5f9;border-color:#cbd5e1}
    .btn-sec:disabled{opacity:.5;cursor:not-allowed}
    .btn-decline{color:#b91c1c;border-color:#fecaca;background:#fff5f5}
    .btn-decline:hover:not(:disabled){background:#fee2e2;border-color:#f87171}

    /* status */
    .status-box{margin-top:14px;padding:13px 16px;border-radius:10px;font-weight:700;
                font-size:14px;text-align:center;display:none;align-items:center;
                justify-content:center;gap:8px}
    .status-box.show{display:flex}
    .status-box.ok{background:#f0fdf4;color:#166534;border:1px solid #bbf7d0}
    .status-box.err{background:#fef2f2;color:#991b1b;border:1px solid #fecaca}
    .status-box.info{background:#eff6ff;color:#1d4ed8;border:1px solid #bfdbfe}

    /* divider */
    .divider{display:flex;align-items:center;gap:10px;margin:14px 0;color:#94a3b8;font-size:11px}
    .divider::before,.divider::after{content:'';flex:1;height:1px;background:#e2e8f0}

    /* footer */
    .page-footer{margin-top:14px;text-align:center;color:rgba(255,255,255,.45);font-size:11px;line-height:1.8}
    .page-footer a{color:rgba(255,255,255,.5);text-decoration:none}
    .secured-by{display:flex;align-items:center;justify-content:center;gap:6px;
                color:rgba(255,255,255,.45);font-size:11px;margin-top:6px}
    .secured-by svg{width:13px;height:13px;fill:currentColor}
  </style>
</head>
<body>

  <!-- top bar -->
  <div class="topbar">
    <div class="topbar-left">
      <div class="topbar-brand">QiCard <span>Payment Gateway</span></div>
      <div class="mock-badge">MOCK</div>
    </div>
    <div class="secure-badge">
      <svg viewBox="0 0 20 20"><path d="M10 1l7 3v5c0 4.4-3 8.5-7 9.9C6 17.5 3 13.4 3 9V4l7-3z"/></svg>
      اتصال آمن SSL
    </div>
  </div>

  <div class="card">

    <!-- order summary -->
    <div class="summary">
      <div class="summary-merchant">
        <div class="merchant-icon">🏢</div>
        <div>
          <div class="merchant-name">مركز التجارة العراقي</div>
          <div class="merchant-sub">ترخيص النظام — {{v.Days}} يوم</div>
        </div>
      </div>
      <div class="summary-amount">
        <div class="amount-value">{{amountFmt}}</div>
        <div class="amount-label">{{v.Currency}}</div>
      </div>
    </div>

    <!-- order details -->
    <div class="details">
      <div class="detail-item">
        <span class="detail-label">رقم الطلب</span>
        <span class="detail-value mono">{{shortId}}…</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">المدّة</span>
        <span class="detail-value">{{v.Days}} يوم</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">العملة</span>
        <span class="detail-value">{{v.Currency}}</span>
      </div>
      <div class="detail-item">
        <span class="detail-label">الحالة</span>
        <span class="detail-value" style="color:#f59e0b">قيد المعالجة</span>
      </div>
    </div>

    <!-- form -->
    <div class="form-body">

      <!-- accepted cards -->
      <div class="networks">
        <div class="net-icon visa">VISA</div>
        <div class="net-icon mc">MC</div>
        <div class="net-icon mada">مدى</div>
        <div class="net-icon qi">QiCard</div>
      </div>

      <!-- card visual -->
      <div class="card-visual" id="cardVisual">
        <div class="cv-top">
          <div class="cv-chip"></div>
          <div class="cv-network">
            <div class="cv-circle"></div>
            <div class="cv-circle"></div>
          </div>
        </div>
        <div class="cv-number" id="cvNumber">•••• •••• •••• ••••</div>
        <div class="cv-bottom">
          <div class="cv-field">
            <div class="cv-field-label">اسم حامل البطاقة</div>
            <div class="cv-field-value" id="cvName">الاسم الكامل</div>
          </div>
          <div class="cv-field" style="text-align:left">
            <div class="cv-field-label">تاريخ الانتهاء</div>
            <div class="cv-field-value" id="cvExpiry">MM / YY</div>
          </div>
        </div>
      </div>

      <div class="section-title">بيانات البطاقة</div>

      <!-- card number -->
      <div class="field">
        <label>رقم البطاقة <span class="req">*</span></label>
        <div class="input-wrap">
          <input type="tel" id="cardNum" maxlength="19" placeholder="0000 0000 0000 0000"
                 autocomplete="cc-number" inputmode="numeric" />
        </div>
        <div class="err-msg" id="errNum">أدخل رقم بطاقة صحيح مكوّن من 16 رقماً</div>
      </div>

      <!-- cardholder name -->
      <div class="field">
        <label>اسم حامل البطاقة <span class="req">*</span></label>
        <div class="input-wrap">
          <input type="text" id="cardName" placeholder="FULL NAME" autocomplete="cc-name"
                 style="text-transform:uppercase" />
        </div>
        <div class="err-msg" id="errName">أدخل الاسم كما هو مطبوع على البطاقة</div>
      </div>

      <!-- expiry + cvv -->
      <div class="field-row">
        <div class="field">
          <label>تاريخ الانتهاء <span class="req">*</span></label>
          <div class="input-wrap">
            <input type="tel" id="cardExp" maxlength="7" placeholder="MM / YY"
                   autocomplete="cc-exp" inputmode="numeric" />
          </div>
          <div class="err-msg" id="errExp">تاريخ غير صحيح</div>
        </div>
        <div class="field">
          <label>CVV <span class="req">*</span></label>
          <div class="input-wrap">
            <input type="tel" id="cardCvv" maxlength="4" placeholder="•••"
                   autocomplete="cc-csc" inputmode="numeric" />
          </div>
          <div class="err-msg" id="errCvv">أدخل CVV صحيح</div>
        </div>
      </div>

      <!-- 3DS notice -->
      <div class="sec-row">
        <svg viewBox="0 0 20 20"><path d="M10 1l7 3v5c0 4.4-3 8.5-7 9.9C6 17.5 3 13.4 3 9V4l7-3z"/></svg>
        هذه العملية محمية بنظام التحقّق الثلاثي (3D Secure)
      </div>

      <!-- pay button -->
      <button class="btn-pay" id="btnPay">
        <span id="btnPayText">ادفع الآن — {{amountFmt}} {{v.Currency}}</span>
        <div class="spinner" id="spinner"></div>
      </button>

      <!-- secondary actions -->
      <div class="sec-actions">
        <button class="btn-sec btn-decline" id="btnFail">رفض البطاقة (محاكاة)</button>
        <button class="btn-sec" id="btnCancel">إلغاء</button>
      </div>

      <!-- status -->
      <div class="status-box" id="statusBox"></div>

      <div class="divider">أو</div>
      <div style="text-align:center;font-size:12px;color:#94a3b8;line-height:1.7">
        وضع تجريبي — لا يُخصم أيّ مبلغ حقيقي.<br/>
        أدخل أي بيانات وهمية أو اضغط زرّ الدفع مباشرةً للمحاكاة.
      </div>

    </div>
  </div>

  <div class="page-footer">
    جميع بيانات البطاقة مشفّرة ومحمية<br/>
    <strong style="color:rgba(255,255,255,.6)">QiCard Payment Gateway</strong> — بيئة تجريبية
  </div>

  <script>
    const sid = "{{sessionId:D}}";

    // ── card visual live update ──
    const cardNum  = document.getElementById('cardNum');
    const cardName = document.getElementById('cardName');
    const cardExp  = document.getElementById('cardExp');

    cardNum.addEventListener('input', e => {
      let v = e.target.value.replace(/\D/g,'').slice(0,16);
      e.target.value = v.replace(/(.{4})/g,'$1 ').trim();
      const masked = (v + '').padEnd(16,'•');
      document.getElementById('cvNumber').textContent =
        masked.slice(0,4)+' '+masked.slice(4,8)+' '+masked.slice(8,12)+' '+masked.slice(12,16);
    });
    cardName.addEventListener('input', e => {
      const val = e.target.value.toUpperCase() || 'الاسم الكامل';
      document.getElementById('cvName').textContent = val.slice(0,22);
    });
    cardExp.addEventListener('input', e => {
      let v = e.target.value.replace(/\D/g,'');
      if (v.length >= 3) v = v.slice(0,2) + ' / ' + v.slice(2,4);
      e.target.value = v;
      document.getElementById('cvExpiry').textContent = v || 'MM / YY';
    });

    // ── validation helpers ──
    function showErr(id, show){ document.getElementById(id).classList.toggle('show', show); }
    function markField(input, errId, ok){
      input.classList.toggle('error', !ok);
      showErr(errId, !ok);
      return ok;
    }

    function validateAndComplete(forcedStatus) {
      if (forcedStatus) { complete(forcedStatus); return; }

      const num = cardNum.value.replace(/\s/g,'');
      const name = cardName.value.trim();
      const exp = cardExp.value.replace(/\s/g,'');
      const cvv = document.getElementById('cardCvv').value.trim();

      let ok = true;
      ok = markField(cardNum,  'errNum',  num.length  === 16) && ok;
      ok = markField(cardName, 'errName', name.length >= 3)   && ok;

      // expiry format MM/YY
      const expMatch = exp.match(/^(\d{2})\/(\d{2})$/);
      const expOk = expMatch && parseInt(expMatch[1]) >= 1 && parseInt(expMatch[1]) <= 12;
      ok = markField(cardExp, 'errExp', !!expOk) && ok;

      ok = markField(document.getElementById('cardCvv'), 'errCvv', cvv.length >= 3) && ok;

      if (ok) complete('SUCCESS');
    }

    async function complete(status){
      const label = status === 'SUCCESS' ? 'تمّ الدفع بنجاح ✓'
                  : status === 'FAILED'  ? 'تمّ رفض البطاقة'
                  :                        'تمّ إلغاء العملية';

      document.querySelectorAll('button').forEach(b => b.disabled = true);
      document.getElementById('btnPayText').style.display = 'none';
      document.getElementById('spinner').style.display = 'block';

      const box = document.getElementById('statusBox');
      box.className = 'status-box show info';
      box.textContent = 'جارٍ معالجة الطلب...';

      try {
        const r = await fetch(`/api/license/qicard/mock/${sid}/complete?status=${status}`,{ method:'POST' });
        const j = await r.json();
        document.getElementById('spinner').style.display = 'none';
        if (j.success) {
          box.className = 'status-box show ok';
          box.textContent = label + ' — سيُغلق هذا التبويب خلال ثانيتين...';
          setTimeout(() => window.close(), 2000);
        } else {
          box.className = 'status-box show err';
          box.textContent = (j.errors || ['خطأ غير متوقع']).join('؛ ');
          document.querySelectorAll('button').forEach(b => b.disabled = false);
          document.getElementById('btnPayText').style.display = 'block';
        }
      } catch {
        document.getElementById('spinner').style.display = 'none';
        box.className = 'status-box show err';
        box.textContent = 'تعذّر الاتصال بالخادم — حاول مرّة أخرى.';
        document.querySelectorAll('button').forEach(b => b.disabled = false);
        document.getElementById('btnPayText').style.display = 'block';
      }
    }

    document.getElementById('btnPay').onclick    = () => validateAndComplete(null);
    document.getElementById('btnFail').onclick   = () => validateAndComplete('FAILED');
    document.getElementById('btnCancel').onclick = () => validateAndComplete('CANCELED');
  </script>
</body>
</html>
""";
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// محاكاة الـ webhook في وضع MOCK. تُستدعى من JS داخل صفحة الـ mock عندما يضغط
    /// المستخدم زرّ "دفع/رفض/إلغاء". تبني payload وهميّ وتُمرّره إلى نفس منطق
    /// <see cref="ICardPaymentService.HandleWebhookAsync"/> ليُحدِّث الجلسة ويُفعّل
    /// الترخيص إن نجح الدفع.
    /// </summary>
    [HttpPost("qicard/mock/{sessionId:guid}/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> QiCardMockComplete(Guid sessionId, [FromQuery] string status, CancellationToken ct)
    {
        if (!_qiOpts.IsMockMode) return NotFound();
        if (string.IsNullOrWhiteSpace(status))
            return BadRequest(new { success = false, errors = new[] { "status query parameter is required." } });

        var fake = new QiWebhookPayload
        {
            PaymentId = sessionId.ToString("N"),
            RequestId = sessionId.ToString("D"),
            Status    = status.ToUpperInvariant(),
            Amount    = null,
            Currency  = null,
            ErrorCode = status.Equals("FAILED", StringComparison.OrdinalIgnoreCase) ? "MOCK_DECLINED" : null,
            ErrorMessage = status.Equals("FAILED", StringComparison.OrdinalIgnoreCase)
                ? "Mock: تمّ رفض البطاقة (محاكاة)"
                : status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase)
                    ? "Mock: ألغى المستخدم العملية (محاكاة)"
                    : null,
        };

        var raw = JsonSerializer.Serialize(fake);
        var r = await _card.HandleWebhookAsync(fake, raw, ct);
        if (!r.IsSuccess)
            return BadRequest(new { success = false, errors = r.Errors });
        return Ok(new { success = true });
    }

    [HttpPost("generate")]
    [RequirePermission(PermissionRegistry.System.License.Generate)]
    public async Task<IActionResult> Generate([FromBody] GenerateDto dto, CancellationToken ct)
    {
        var code = await _svc.GenerateAsync(dto.Days, ct);
        return Ok(new { success = true, data = new { code, days = dto.Days } });
    }

    /// <summary>
    /// (اختبار) إنهاء الترخيص فوراً ليعمل النظام في وضع "قراءة فقط". مخصّص لاختبار
    /// السلوك بدون انتظار الانتهاء الفعلي. محصور بصلاحية <c>License.Generate</c>.
    /// </summary>
    [HttpPost("test-expire")]
    [RequirePermission(PermissionRegistry.System.License.Generate)]
    public async Task<IActionResult> TestExpire([FromBody] TestExpireDto? dto, CancellationToken ct)
    {
        var r = await _svc.TestExpireAsync(CurrentUserId(), dto?.ExpireType, ct);
        if (r.IsSuccess) LicenseEnforcementMiddleware.InvalidateCache();
        return HandleResult(r);
    }

    /// <summary>
    /// (اختبار) إعادة الترخيص للوضع النشط بمدة افتراضية 30 يوم. محصور بصلاحية
    /// <c>License.Generate</c>.
    /// </summary>
    [HttpPost("test-restore")]
    [RequirePermission(PermissionRegistry.System.License.Generate)]
    public async Task<IActionResult> TestRestore([FromBody] TestRestoreDto? dto, CancellationToken ct)
    {
        var days = dto?.Days is > 0 ? dto.Days : 30;
        var r = await _svc.TestRestoreAsync(days, CurrentUserId(), ct);
        if (r.IsSuccess) LicenseEnforcementMiddleware.InvalidateCache();
        return HandleResult(r);
    }

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    public sealed class ApplyCodeDto    { public string? Code { get; set; } }
    public sealed class BuyDto          { public int Days { get; set; } }
    public sealed class GenerateDto     { public int Days { get; set; } }
    public sealed class TestRestoreDto  { public int Days { get; set; } = 30; }
    /// <summary>
    /// نوع الإنهاء التجريبي:
    ///   • <c>null / "natural"</c> — انتهى منذ يوم (افتراضي)
    ///   • <c>"canceled"</c>       — إلغاء إداري — منذ 30 يوماً
    ///   • <c>"warning"</c>        — شارف على الانتهاء (+ 3 أيام)
    /// </summary>
    public sealed class TestExpireDto   { public string? ExpireType { get; set; } }
}
