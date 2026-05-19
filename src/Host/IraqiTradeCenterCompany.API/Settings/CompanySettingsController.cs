using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Settings;

[ApiController]
[Authorize]
[Route("api/company-settings")]
public class CompanySettingsController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CompanySettingsController(AuthDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get()
    {
        var s = await _db.CompanySettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s == null)
        {
            s = new CompanySettings
            {
                Id = 1,
                NameAr = "مركز التجارة العراقي",
                NameEn = "Iraqi Trade Center",
                Currency = "IQD",
                UpdatedAt = DateTime.UtcNow
            };
            _db.CompanySettings.Add(s);
            await _db.SaveChangesAsync();
        }
        return Ok(new { success = true, data = s });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateCompanySettingsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return BadRequest(new { success = false, message = "اسم الشركة مطلوب" });

        // حد أقصى ~5MB لداتا اللوكو
        if (!string.IsNullOrEmpty(req.LogoBase64) && req.LogoBase64.Length > 7_000_000)
            return BadRequest(new { success = false, message = "حجم الشعار أكبر من المسموح به" });

        var s = await _db.CompanySettings.FirstOrDefaultAsync(x => x.Id == 1);
        if (s == null)
        {
            s = new CompanySettings { Id = 1 };
            _db.CompanySettings.Add(s);
        }

        s.NameAr       = req.NameAr.Trim();
        s.NameEn       = req.NameEn?.Trim();
        s.Address      = req.Address?.Trim();
        s.Phone        = req.Phone?.Trim();
        s.Email        = req.Email?.Trim();
        s.Website      = req.Website?.Trim();
        s.TaxNumber    = req.TaxNumber?.Trim();
        s.Currency     = string.IsNullOrWhiteSpace(req.Currency) ? "IQD" : req.Currency.Trim().ToUpperInvariant();
        s.ExchangeRatesJson = string.IsNullOrWhiteSpace(req.ExchangeRatesJson) ? null : req.ExchangeRatesJson.Trim();
        s.LogoBase64   = req.LogoBase64;
        s.PrintHeader  = req.PrintHeader?.Trim();
        s.PrintFooter  = req.PrintFooter?.Trim();
        s.UpdatedAt    = DateTime.UtcNow;
        s.UpdatedBy    = _currentUser.UserId?.ToString() ?? "system";

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = s });
    }
}

public record UpdateCompanySettingsRequest(
    string NameAr,
    string? NameEn,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? TaxNumber,
    string? Currency,
    string? ExchangeRatesJson,
    string? LogoBase64,
    string? PrintHeader,
    string? PrintFooter
);
