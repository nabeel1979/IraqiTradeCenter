using Microsoft.Data.SqlClient;
using IraqiTradeCenterCompany.SharedKernel.Models;

namespace IraqiTradeCenterCompany.API.Licensing;

/// <summary>
/// إعدادات الترخيص الأساسية للشركة الحالية — صف واحد فقط في <c>sys.LicenseConfig</c>.
/// </summary>
public sealed class LicenseConfig
{
    public required string CompanyKey { get; init; }
    public required string AuthKey    { get; init; }
    public decimal PricePerDay { get; init; }
    public string  Currency    { get; init; } = "IQD";
    public int     GraceDays   { get; init; } = 3;
}

/// <summary>الحالة الحالية للترخيص (الانتهاء + العدّاد).</summary>
public sealed class LicenseStatus
{
    public required string CompanyKey { get; init; }
    /// <summary>تاريخ انتهاء آخر تفعيل (UTC). <c>null</c> لو لم يُفعَّل أبداً.</summary>
    public DateTime? EndDateUtc { get; init; }
    public int DaysRemaining { get; init; }
    public bool IsActive { get; init; }
    public bool IsInGrace { get; init; }
    public bool IsExpired { get; init; }
    public string? LastCode { get; init; }
    public decimal PricePerDay { get; init; }
    public string  Currency    { get; init; } = "IQD";
    public decimal WalletBalance { get; init; }
}

public sealed class ActivationRow
{
    public int      Id         { get; set; }
    public string   Code       { get; set; } = "";
    public int      Days       { get; set; }
    public DateTime StartDate  { get; set; }
    public DateTime EndDate    { get; set; }
    public DateTime AppliedAt  { get; set; }
    public string?  AppliedBy  { get; set; }
    public string   Source     { get; set; } = "Code";
    public string?  Note       { get; set; }
}

public interface ILicenseService
{
    Task<LicenseConfig>     GetConfigAsync(CancellationToken ct);
    Task<LicenseStatus>     GetStatusAsync(CancellationToken ct);
    Task<List<ActivationRow>> GetHistoryAsync(int take, CancellationToken ct);
    Task<Result<ActivationRow>> ApplyCodeAsync(string code, string source, string? userId, CancellationToken ct);
    /// <summary>توليد شفرة (للاستعمال الإداري المؤقت — Parent SP لاحقاً يحلّ محلّها).</summary>
    Task<string> GenerateAsync(int days, CancellationToken ct);
}

public class LicenseService : ILicenseService
{
    private readonly IConfiguration _cfg;
    public LicenseService(IConfiguration cfg) { _cfg = cfg; }

    private SqlConnection Open()
    {
        var cs = _cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection");
        var cn = new SqlConnection(cs);
        cn.Open();
        return cn;
    }

    public async Task<LicenseConfig> GetConfigAsync(CancellationToken ct)
    {
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT TOP 1 CompanyKey, AuthKey, PricePerDay, Currency, GraceDays
FROM [licensing].[LicenseConfig] WHERE Id = 1;";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            throw new InvalidOperationException("LicenseConfig is not initialized.");
        return new LicenseConfig
        {
            CompanyKey  = r.GetString(0),
            AuthKey     = r.GetString(1),
            PricePerDay = r.GetDecimal(2),
            Currency    = r.GetString(3),
            GraceDays   = r.GetInt32(4),
        };
    }

    public async Task<LicenseStatus> GetStatusAsync(CancellationToken ct)
    {
        var cfg = await GetConfigAsync(ct);
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT TOP 1 EndDate, Code FROM [licensing].[LicenseActivations]
ORDER BY EndDate DESC, Id DESC;";
        DateTime? endUtc = null;
        string?   lastCode = null;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (await r.ReadAsync(ct))
            {
                endUtc = DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc);
                lastCode = r.GetString(1);
            }
        }

        decimal balance;
        await using (var cmd2 = cn.CreateCommand())
        {
            cmd2.CommandText = "SELECT Balance FROM [licensing].[Wallet] WHERE Id = 1;";
            balance = (decimal)((await cmd2.ExecuteScalarAsync(ct)) ?? 0m);
        }

        var now = DateTime.UtcNow;
        int days = 0;
        bool isActive = false;
        bool inGrace = false;
        bool isExpired = false;
        if (endUtc != null)
        {
            var delta = (endUtc.Value - now).TotalDays;
            days = (int)Math.Floor(delta);
            if (days >= 0) { isActive = true; }
            else
            {
                isExpired = true;
                // ‎فترة سماح بعد الانتهاء بأيام قليلة (للسماح بشراء وتجديد).
                if (-days <= cfg.GraceDays) inGrace = true;
            }
        }
        else
        {
            isExpired = true;
        }

        return new LicenseStatus
        {
            CompanyKey    = cfg.CompanyKey,
            EndDateUtc    = endUtc,
            DaysRemaining = Math.Max(days, 0),
            IsActive      = isActive,
            IsInGrace     = inGrace,
            IsExpired     = isExpired && !isActive,
            LastCode      = lastCode,
            PricePerDay   = cfg.PricePerDay,
            Currency      = cfg.Currency,
            WalletBalance = balance,
        };
    }

    public async Task<List<ActivationRow>> GetHistoryAsync(int take, CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"
SELECT TOP {take} Id, Code, Days, StartDate, EndDate, AppliedAt, AppliedBy, Source, Note
FROM [licensing].[LicenseActivations]
ORDER BY Id DESC;";
        var rows = new List<ActivationRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            rows.Add(new ActivationRow
            {
                Id        = r.GetInt32(0),
                Code      = r.GetString(1),
                Days      = r.GetInt32(2),
                StartDate = DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc),
                EndDate   = DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc),
                AppliedAt = DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc),
                AppliedBy = r.IsDBNull(6) ? null : r.GetString(6),
                Source    = r.GetString(7),
                Note      = r.IsDBNull(8) ? null : r.GetString(8),
            });
        }
        return rows;
    }

    public async Task<Result<ActivationRow>> ApplyCodeAsync(
        string code, string source, string? userId, CancellationToken ct)
    {
        var cfg = await GetConfigAsync(ct);
        if (!LicenseCode.TryParseAndVerify(code, cfg.CompanyKey, cfg.AuthKey,
                out var days, out var expiryUtc, out var err))
        {
            return Result.Failure<ActivationRow>(err ?? "الشفرة غير صالحة");
        }

        var normalized = code.Trim().ToUpperInvariant();

        await using var cn = Open();
        // ‎منع إعادة استخدام نفس الشفرة (الـ unique index سيُحبط هذا أيضاً).
        await using (var dup = cn.CreateCommand())
        {
            dup.CommandText = "SELECT TOP 1 Id FROM [licensing].[LicenseActivations] WHERE Code = @c";
            dup.Parameters.AddWithValue("@c", normalized);
            var existing = await dup.ExecuteScalarAsync(ct);
            if (existing != null && existing != DBNull.Value)
                return Result.Failure<ActivationRow>("هذه الشفرة طُبِّقت مسبقاً ولا يمكن إعادة استخدامها.");
        }

        // ‎الحساب: إذا كان هناك ترخيص نشط حالياً، نُكدّس الأيام الجديدة على EndDate
        // ‎الحالي (تجميع المدد). وإلا نبدأ من الآن.
        DateTime startUtc;
        DateTime endUtc;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"SELECT TOP 1 EndDate FROM [licensing].[LicenseActivations]
                                ORDER BY EndDate DESC, Id DESC;";
            var cur = await cmd.ExecuteScalarAsync(ct);
            var now = DateTime.UtcNow;
            if (cur != null && cur != DBNull.Value)
            {
                var currentEnd = DateTime.SpecifyKind((DateTime)cur, DateTimeKind.Utc);
                startUtc = currentEnd > now ? currentEnd : now;
            }
            else startUtc = now;
            endUtc = startUtc.AddDays(days);
        }

        await using (var ins = cn.CreateCommand())
        {
            ins.CommandText = @"
INSERT INTO [licensing].[LicenseActivations]
    ([Code],[CompanyKey],[Days],[StartDate],[EndDate],[AppliedAt],[AppliedBy],[Source])
OUTPUT INSERTED.Id, INSERTED.AppliedAt
VALUES (@c, @ck, @d, @s, @e, SYSUTCDATETIME(), @u, @src);";
            ins.Parameters.AddWithValue("@c",   normalized);
            ins.Parameters.AddWithValue("@ck",  cfg.CompanyKey);
            ins.Parameters.AddWithValue("@d",   days);
            ins.Parameters.AddWithValue("@s",   startUtc);
            ins.Parameters.AddWithValue("@e",   endUtc);
            ins.Parameters.AddWithValue("@u",   (object?)userId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@src", source);
            await using var rr = await ins.ExecuteReaderAsync(ct);
            await rr.ReadAsync(ct);
            var row = new ActivationRow
            {
                Id        = rr.GetInt32(0),
                Code      = normalized,
                Days      = days,
                StartDate = startUtc,
                EndDate   = endUtc,
                AppliedAt = DateTime.SpecifyKind(rr.GetDateTime(1), DateTimeKind.Utc),
                AppliedBy = userId,
                Source    = source,
            };
            return Result.Success(row);
        }
    }

    public async Task<string> GenerateAsync(int days, CancellationToken ct)
    {
        var cfg = await GetConfigAsync(ct);
        if (days <= 0) days = 30;
        if (days > 3650) days = 3650;
        // ‎تاريخ الانتهاء لتوقيع الشفرة: نقصر العمر على 365 يوم من تاريخ الإصدار
        // ‎كي لا تُستعمل شفرات قديمة جداً (لا علاقة له بحساب EndDate الفعلي عند
        // ‎التطبيق — ذاك يستعمل المدة وحدها بداية من تاريخ التطبيق).
        var expiryUtc = DateTime.UtcNow.AddDays(365).Date;
        var code = LicenseCode.Generate(cfg.CompanyKey, days, expiryUtc, cfg.AuthKey);
        await Task.CompletedTask;
        return code;
    }
}
