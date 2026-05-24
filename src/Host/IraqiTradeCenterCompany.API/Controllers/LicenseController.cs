using System.Security.Claims;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Licensing;
using IraqiTradeCenterCompany.SharedKernel.Models;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ILicenseService _svc;
    private readonly IWalletService  _wallet;

    public LicenseController(ILicenseService svc, IWalletService wallet)
    {
        _svc    = svc;
        _wallet = wallet;
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

    [HttpPost("buy-with-card")]
    [RequirePermission(PermissionRegistry.System.License.Apply)]
    public async Task<IActionResult> BuyWithCard([FromBody] BuyDto dto, CancellationToken ct)
    {
        if (dto.Days <= 0)
            return BadRequest(new { success = false, errors = new[] { "عدد الأيام يجب أن يكون أكبر من صفر." } });

        var cfg = await _svc.GetConfigAsync(ct);
        var amount = cfg.PricePerDay * dto.Days;
        // ‎هذا placeholder حتى ندمج بوّابة دفع فعلية — نُسجّل طلب دفع بحالة Pending
        // ‎ويُمكن للمسؤول لاحقاً موافقته يدوياً (يُولِّد شفرة عند الموافقة).
        // ‎الـ MVP يُعيد الطلب فقط — التنفيذ الكامل في طلب لاحق.
        return Ok(new
        {
            success = true,
            data = new
            {
                method = "Card",
                amount,
                currency = cfg.Currency,
                days = dto.Days,
                status = "Pending",
                message = "بوّابة الدفع بالبطاقة قيد التكامل. يُمكن الشراء حالياً من خلال المحفظة أو إدخال شفرة من مركز التجارة العراقي.",
            }
        });
    }

    [HttpPost("generate")]
    [RequirePermission(PermissionRegistry.System.License.Generate)]
    public async Task<IActionResult> Generate([FromBody] GenerateDto dto, CancellationToken ct)
    {
        var code = await _svc.GenerateAsync(dto.Days, ct);
        return Ok(new { success = true, data = new { code, days = dto.Days } });
    }

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    public sealed class ApplyCodeDto { public string? Code { get; set; } }
    public sealed class BuyDto       { public int Days { get; set; } }
    public sealed class GenerateDto  { public int Days { get; set; } }
}
