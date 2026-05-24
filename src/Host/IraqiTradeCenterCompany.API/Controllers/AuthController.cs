using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
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
    private readonly IPermissionService _permissions;

    public AuthController(AuthDbContext db, IConfiguration config, IPermissionService permissions)
    {
        _db = db;
        _config = config;
        _permissions = permissions;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Phone) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { success = false, errors = new[] { "اسم المستخدم وكلمة المرور مطلوبان" } });

        // ‎الحقل القادم من الواجهة قد يكون رقم هاتف أو اسم مستخدم أو الاسم الكامل.
        // ‎نطابقه على أي من الحقلين Phone/FullName بشكل حسّاس للحالة (الـ DB collation
        // ‎هو Arabic_CI_AS فالمطابقة insensitive افتراضياً للنصوص العربية والإنجليزية).
        var identifier = req.Phone.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            (u.Phone == identifier || u.FullName == identifier) && u.IsActive);
        if (user is null || !PasswordHelper.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { success = false, errors = new[] { "اسم المستخدم أو كلمة المرور غير صحيحة" } });

        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:ExpirationHours", 24));

        // جمع أدواره وصلاحياته الفعلية (الـ override + الـ super check محسوبَيْن داخلياً)
        var roleCodes = await _db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(_db.Roles.Where(r => r.IsActive), ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
            .ToListAsync();
        if (roleCodes.Count == 0) roleCodes.Add(user.Role); // backward compat

        var perms = await _permissions.GetUserPermissionsAsync(user.Id);
        var isSuper = await _permissions.IsSuperAdminAsync(user.Id);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier,   user.Id.ToString()),
            new(ClaimTypes.Name,             user.FullName),
            new("phone",                     user.Phone),
            new("companyId",                 "1"),
        };
        foreach (var rc in roleCodes.Distinct())
            claims.Add(new Claim(ClaimTypes.Role, rc));
        if (isSuper && !roleCodes.Contains("SuperAdmin"))
            claims.Add(new Claim(ClaimTypes.Role, "SuperAdmin"));

        // ضع الصلاحيات في الـ token فقط إذا لم يكن SuperAdmin (الـ token سيكبر بلا داعٍ)
        if (!isSuper)
        {
            foreach (var p in perms)
                claims.Add(new Claim("perm", p));
        }

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
                user      = new
                {
                    id          = user.Id,
                    fullName    = user.FullName,
                    phone       = user.Phone,
                    role        = user.Role,
                    roles       = roleCodes,
                    permissions = perms.ToArray(),
                    isSuperAdmin = isSuper,
                }
            }
        });
    }
}

public record LoginRequest(string Phone, string Password);
