using System.Text.Json;

namespace IraqiTradeCenterCompany.API.Licensing;

/// <summary>
/// عند انتهاء الترخيص يدخل النظام وضع <c>قراءة فقط</c>:
///   • <c>GET</c>/<c>HEAD</c>/<c>OPTIONS</c> مسموحة (عرض، تقارير، طباعة).
///   • <c>POST</c>/<c>PUT</c>/<c>PATCH</c>/<c>DELETE</c> محجوبة بـ <c>403 LICENSE_EXPIRED</c>.
///
/// مسارات تبقى مفتوحة دائماً لكلّ الأفعال (حتى الكتابة) لأنها ضرورية لاستعادة
/// الخدمة وتطبيق شفرة جديدة:
///   • /api/license/*  — قراءة الحالة وتطبيق شفرة جديدة
///   • /api/wallet/*   — شحن المحفظة
///   • /api/auth/*     — تسجيل الدخول
///   • /api/users/me   — جلب المستخدم الحالي
///   • /api/company-settings — هوية الشركة (شعار، اسم) لشاشة القفل
///   • /health، /swagger، /
///
/// لأداء أفضل: نتذكّر آخر <see cref="DateTime"/> لانتهاء الترخيص لمدّة 60 ثانية كحد أعلى،
/// كي لا نضرب قاعدة البيانات مع كل طلب.
/// </summary>
public class LicenseEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LicenseEnforcementMiddleware> _log;

    // ‎كاش بسيط في الذاكرة: قيمة EndDate + متى قرأناها.
    private static DateTime? _cachedEndUtc;
    private static DateTime  _cachedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly object _lock = new();

    public LicenseEnforcementMiddleware(RequestDelegate next, ILogger<LicenseEnforcementMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx, ILicenseService license)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (IsAllowlisted(path))
        {
            await _next(ctx);
            return;
        }

        var endUtc = await GetEndUtcAsync(license, ctx.RequestAborted);
        var now = DateTime.UtcNow;
        if (endUtc == null || endUtc.Value <= now)
        {
            // ‎الترخيص منتهٍ — اسمح بأفعال القراءة (GET/HEAD/OPTIONS) كي يبقى
            // ‎النظام يعرض البيانات ويطبعها، واحجب فقط أفعال الكتابة.
            if (IsReadOnlyMethod(ctx.Request.Method))
            {
                ctx.Response.Headers["X-License-Status"] = "expired-readonly";
                await _next(ctx);
                return;
            }

            await WriteExpiredAsync(ctx, endUtc);
            return;
        }
        await _next(ctx);
    }

    /// <summary>أفعال HTTP لا تُعدِّل الحالة — مسموحة عند انتهاء الترخيص.</summary>
    private static bool IsReadOnlyMethod(string method) =>
        HttpMethods.IsGet(method) ||
        HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method);

    /// <summary>المسارات المسموح بها دائماً (حتى عند انتهاء الترخيص).</summary>
    private static bool IsAllowlisted(string path)
    {
        // ‎نلتقط /api/license, /api/wallet (لشحن المحفظة), /api/auth, /api/users/me,
        // ‎/api/company-settings (للهوية), بالإضافة لمسارات الصحّة و swagger.
        if (path.StartsWith("/api/license",         StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/api/wallet",          StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/api/auth",            StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/api/users/me",        StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/api/company-settings",StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/health",              StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/swagger",             StringComparison.OrdinalIgnoreCase)) return true;
        if (path == "/" || path == "") return true;
        return false;
    }

    private static async Task<DateTime?> GetEndUtcAsync(ILicenseService svc, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        // ‎قراءة من الكاش بدون lock أوّلاً (سريع)
        if (now - _cachedAtUtc < CacheTtl) return _cachedEndUtc;

        // ‎تحديث مع lock — مرّة واحدة عبر كل threads
        var status = await svc.GetStatusAsync(ct);
        lock (_lock)
        {
            _cachedEndUtc = status.EndDateUtc;
            _cachedAtUtc  = now;
        }
        return status.EndDateUtc;
    }

    /// <summary>إبطال الكاش — يُستدعى من الـ controller بعد تطبيق شفرة جديدة.</summary>
    public static void InvalidateCache()
    {
        lock (_lock) { _cachedAtUtc = DateTime.MinValue; }
    }

    private static async Task WriteExpiredAsync(HttpContext ctx, DateTime? endUtc)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.Headers["X-License-Status"] = "expired-readonly";

        var message = endUtc == null
            ? "النظام غير مفعَّل — لا يمكن إجراء أي عملية حفظ أو تعديل أو حذف. يجب تطبيق شفرة ترخيص للبدء."
            : $"انتهى ترخيص النظام بتاريخ {endUtc:yyyy-MM-dd}. النظام في وضع القراءة فقط — لا يمكن الحفظ/التعديل/الحذف حتى تطبيق شفرة جديدة.";

        // ‎نُرجع الرسالة في `message` للسلوك التاريخي، وفي `errors[]` كي يلتقطها
        // ‎الـ axios interceptor العام ويعرضها كـ toast بدون تخصيص إضافي.
        var body = JsonSerializer.Serialize(new
        {
            success  = false,
            code     = "LICENSE_EXPIRED",
            message,
            errors   = new[] { message },
            endDate  = endUtc,
            readOnly = true,
        });
        await ctx.Response.WriteAsync(body, ctx.RequestAborted);
    }
}
