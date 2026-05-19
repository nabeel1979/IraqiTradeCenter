using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IraqiTradeCenterCompany.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IraqiTradeCenterCompany.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AuthDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, errors = new[] { "رقم الهاتف وكلمة المرور مطلوبان" } });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == req.Phone && u.IsActive);
        if (user is null || !PasswordHelper.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { success = false, errors = new[] { "رقم الهاتف أو كلمة المرور غير صحيحة" } });

        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:ExpirationHours", 24));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier,   user.Id.ToString()),
            new Claim(ClaimTypes.Name,             user.FullName),
            new Claim("phone",                     user.Phone),
            new Claim(ClaimTypes.Role,             user.Role),
            new Claim("companyId",                 "1"),
        };

        var token = new JwtSecurityToken(
            issuer:            _config["Jwt:Issuer"],
            audience:          _config["Jwt:Audience"],
            claims:            claims,
            expires:           expiry,
            signingCredentials: creds);

        return Ok(new
        {
            success = true,
            data = new
            {
                token     = new JwtSecurityTokenHandler().WriteToken(token),
                expiresAt = expiry.ToString("O"),
                user      = new { id = user.Id, fullName = user.FullName, phone = user.Phone, role = user.Role }
            }
        });
    }
}

public record LoginRequest(string Phone, string Password);
