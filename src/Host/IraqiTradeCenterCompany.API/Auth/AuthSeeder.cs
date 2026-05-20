using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Auth;

public static class AuthSeeder
{
    public static async Task SeedAsync(AuthDbContext db, IConfiguration config)
    {
        if (await db.Users.AnyAsync()) return;

        var phone = config["InitialAdmin:Phone"] ?? "admin";
        var name  = config["InitialAdmin:FullName"] ?? "المدير العام";
        var pass  = config["InitialAdmin:Password"] ?? "Admin@2024";

        db.Users.Add(new CompanyUser
        {
            Id           = Guid.NewGuid(),
            FullName     = name,
            Phone        = phone,
            PasswordHash = PasswordHelper.Hash(pass),
            Role         = "SuperAdmin",
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
