using System.Security.Claims;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Licensing;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// محفظة الشركة المالية — تُستخدم حالياً لشراء التراخيص. الـ MVP يدعم:
///   • GET  /api/wallet/status — رصيد + عملة
///   • GET  /api/wallet/transactions — السجلّ
///   • POST /api/wallet/topup — شحن (محتجَز لمسؤولي النظام؛ في الإنتاج
///     يصبح هذا من خلال بوّابة دفع، حالياً placeholder).
/// </summary>
public class WalletController : BaseApiController
{
    private readonly IWalletService _wallet;
    public WalletController(IWalletService wallet) { _wallet = wallet; }

    [HttpGet("status")]
    [RequirePermission(PermissionRegistry.System.Wallet.Read)]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(new { success = true, data = await _wallet.GetAsync(ct) });

    [HttpGet("transactions")]
    [RequirePermission(PermissionRegistry.System.Wallet.Read)]
    public async Task<IActionResult> Transactions([FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(new { success = true, data = await _wallet.GetTransactionsAsync(take, ct) });

    [HttpPost("topup")]
    [RequirePermission(PermissionRegistry.System.Wallet.Topup)]
    public async Task<IActionResult> Topup([FromBody] TopupDto dto, CancellationToken ct)
    {
        if (dto.Amount <= 0)
            return BadRequest(new { success = false, errors = new[] { "المبلغ يجب أن يكون أكبر من صفر." } });
        var r = await _wallet.TopupAsync(dto.Amount, "Topup", dto.Reference, dto.Note, CurrentUserId(), ct);
        return r.IsSuccess
            ? Ok(new { success = true, data = new { balance = r.Value } })
            : BadRequest(new { success = false, errors = r.Errors });
    }

    private string? CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub");

    public sealed class TopupDto
    {
        public decimal Amount    { get; set; }
        public string? Reference { get; set; }
        public string? Note      { get; set; }
    }
}
