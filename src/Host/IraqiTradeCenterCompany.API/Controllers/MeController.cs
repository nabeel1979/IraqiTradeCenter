using System.Security.Claims;
using IraqiTradeCenterCompany.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController : ControllerBase
{
    private readonly AuthDbContext _db;

    public MeController(AuthDbContext db)
    {
        _db = db;
    }

    // ‎الحجم الأقصى المسموح به للتفضيلات (16 KB) — يحمي من تسريب أو هجوم
    private const int MaxPreferencesBytes = 16 * 1024;

    /// <summary>يرجع JSON التفضيلات المخزّن للمستخدم الحالي (أو {} إن لم يضبطه بعد).</summary>
    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, errors = new[] { "Invalid token" } });

        var prefs = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Preferences)
            .FirstOrDefaultAsync();

        return Ok(new { success = true, data = prefs ?? "{}" });
    }

    /// <summary>يحفظ تفضيلات المستخدم الحالي. الـ body عبارة عن JSON كنص (string).</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest body)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { success = false, errors = new[] { "Invalid token" } });

        var raw = body?.Preferences ?? string.Empty;

        if (System.Text.Encoding.UTF8.GetByteCount(raw) > MaxPreferencesBytes)
            return BadRequest(new { success = false, errors = new[] { "Preferences too large" } });

        // ‎تأكد إنه JSON صالح حتى لا يتلوّث الـ DB بقيم غير صالحة
        try { using var _ = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw); }
        catch { return BadRequest(new { success = false, errors = new[] { "Preferences must be valid JSON" } }); }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return NotFound(new { success = false, errors = new[] { "User not found" } });

        user.Preferences = string.IsNullOrWhiteSpace(raw) ? null : raw;
        await _db.SaveChangesAsync();

        return Ok(new { success = true, data = user.Preferences ?? "{}" });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}

public record UpdatePreferencesRequest(string Preferences);
