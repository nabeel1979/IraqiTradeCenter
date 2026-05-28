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
    /// <summary>
    /// <c>true</c> لو رصدنا تلاعباً بسلسلة تواقيع التفعيلات (تعديل DB مباشر).
    /// عندها يُعامَل النظام كمنتهٍ بصرف النظر عن قيم <c>EndDate</c> المخزَّنة.
    /// </summary>
    public bool IsTampered { get; init; }
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

    /// <summary>
    /// (اختبار فقط) يضع <c>EndDate</c> لآخر تفعيل في الماضي فيدخل النظام وضع "قراءة فقط".
    /// يُعيد توقيع السلسلة كاملةً ليبقى التحقّق متّسقاً.
    /// <para>
    /// <paramref name="expireType"/>:
    /// <list type="bullet">
    ///   <item><c>"natural"</c> — انتهى منذ يوم واحد (افتراضي)</item>
    ///   <item><c>"canceled"</c> — إلغاء إداري — منذ 30 يوماً</item>
    ///   <item><c>"warning"</c> — شارف على الانتهاء (+ 3 أيام من الآن)</item>
    /// </list>
    /// </para>
    /// </summary>
    Task<Result> TestExpireAsync(string? userId, string? expireType, CancellationToken ct);

    /// <summary>
    /// (اختبار فقط) يضع <c>EndDate</c> لآخر تفعيل بعد <paramref name="days"/> يوم
    /// من الآن (افتراضياً 30) فيعود النظام للوضع النشط. يُعيد توقيع السلسلة.
    /// </summary>
    Task<Result> TestRestoreAsync(int days, string? userId, CancellationToken ct);
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

        // ‎اقرأ السجلات بالترتيب التصاعدي، تحقّق من سلسلة التواقيع، ثم خذ آخر EndDate.
        // ‎لو السلسلة مكسورة (تلاعب بـ DB) → النظام يُعتبر منتهٍ نهائياً.
        var (endUtc, lastCode, tampered) = await ReadVerifiedEndDateAsync(cn, cfg.AuthKey, ct);

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
        if (tampered)
        {
            // ‎ترخيص مزوَّر → نتعامل معه كمنتهٍ بدون فترة سماح.
            isExpired = true;
            endUtc = null;
        }
        else if (endUtc != null)
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
            IsTampered    = tampered,
            LastCode      = lastCode,
            PricePerDay   = cfg.PricePerDay,
            Currency      = cfg.Currency,
            WalletBalance = balance,
        };
    }

    /// <summary>
    /// يتحقّق من سلسلة تواقيع التفعيلات (Hash Chain) ثم يُرجع آخر <c>EndDate</c>
    /// الفعلية. لو أيّ صفّ كسر السلسلة → نُرجع <c>tampered = true</c>.
    /// </summary>
    private static async Task<(DateTime? EndUtc, string? LastCode, bool Tampered)>
        ReadVerifiedEndDateAsync(SqlConnection cn, string authKey, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT Id, Code, Days, StartDate, EndDate, AppliedAt, RowSig
FROM [licensing].[LicenseActivations]
ORDER BY Id ASC;";

        DateTime? endUtc = null;
        string?   lastCode = null;
        var prev = LicenseChainSigner.Genesis;

        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var code      = r.GetString(1);
            var days      = r.GetInt32(2);
            var startDate = DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc);
            var endDate   = DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc);
            var appliedAt = DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc);
            var storedSig = r.IsDBNull(6) ? null : r.GetString(6);

            // ‎صفّ بدون توقيع = تلاعب (إدراج مباشر بعد ترقية الـ schema).
            if (string.IsNullOrEmpty(storedSig))
                return (null, null, true);

            var expected = LicenseChainSigner.ComputeRowSig(
                authKey, code, days, startDate, endDate, appliedAt, prev);

            if (!string.Equals(expected, storedSig, StringComparison.OrdinalIgnoreCase))
                return (null, null, true);

            // ‎تابع السلسلة وتتبَّع آخر EndDate (نختار الأكبر لأن EndDate قد يكدِّس).
            if (endUtc == null || endDate > endUtc.Value)
            {
                endUtc   = endDate;
                lastCode = code;
            }
            prev = storedSig;
        }

        return (endUtc, lastCode, false);
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

        // ‎اقرأ آخر RowSig لتمديد سلسلة التواقيع. لو لم يوجد سجلات → genesis.
        string prevSig = LicenseChainSigner.Genesis;
        await using (var prev = cn.CreateCommand())
        {
            prev.CommandText = @"SELECT TOP 1 RowSig FROM [licensing].[LicenseActivations]
                                 ORDER BY Id DESC;";
            var p = await prev.ExecuteScalarAsync(ct);
            if (p != null && p != DBNull.Value && !string.IsNullOrEmpty((string)p))
                prevSig = (string)p;
        }

        ActivationRow row;
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
            row = new ActivationRow
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
        }

        // ‎احسب RowSig بناءً على prevSig + قيم الصفّ المُدرَج، ثم خزّنه.
        var rowSig = LicenseChainSigner.ComputeRowSig(
            cfg.AuthKey, row.Code, row.Days, row.StartDate, row.EndDate, row.AppliedAt, prevSig);

        await using (var sig = cn.CreateCommand())
        {
            sig.CommandText = @"UPDATE [licensing].[LicenseActivations] SET RowSig = @s WHERE Id = @id;";
            sig.Parameters.AddWithValue("@s",  rowSig);
            sig.Parameters.AddWithValue("@id", row.Id);
            await sig.ExecuteNonQueryAsync(ct);
        }

        return Result.Success(row);
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

    public Task<Result> TestExpireAsync(string? userId, string? expireType, CancellationToken ct)
    {
        return expireType?.ToLowerInvariant() switch
        {
            "canceled" => SetLastEndDateAsync(DateTime.UtcNow.AddDays(-30), userId, "TEST_CANCEL",  ct),
            "warning"  => SetLastEndDateAsync(DateTime.UtcNow.AddDays(+3),  userId, "TEST_WARNING", ct),
            _          => SetLastEndDateAsync(DateTime.UtcNow.AddDays(-1),   userId, "TEST_EXPIRE",  ct),
        };
    }

    public Task<Result> TestRestoreAsync(int days, string? userId, CancellationToken ct)
    {
        if (days <= 0)   days = 30;
        if (days > 3650) days = 3650;
        return SetLastEndDateAsync(DateTime.UtcNow.AddDays(days), userId, "TEST_RESTORE", ct);
    }

    /// <summary>
    /// (داخلي — للاختبار فقط) يُعدّل <c>EndDate</c> لآخر تفعيل ثم يُعيد توقيع
    /// السلسلة كاملةً. لو لا توجد تفعيلات، نُدرج صفّاً اختباري بشفرة TEST_*.
    /// </summary>
    private async Task<Result> SetLastEndDateAsync(
        DateTime newEndUtc, string? userId, string mode, CancellationToken ct)
    {
        var cfg = await GetConfigAsync(ct);
        await using var cn = Open();

        // ‎التقط آخر صفّ. لو لا يوجد → أنشئ صفّاً اختبارياً جديداً.
        int? lastId = null;
        await using (var pick = cn.CreateCommand())
        {
            pick.CommandText = "SELECT TOP 1 Id FROM [licensing].[LicenseActivations] ORDER BY Id DESC;";
            var o = await pick.ExecuteScalarAsync(ct);
            if (o != null && o != DBNull.Value) lastId = (int)o;
        }

        if (lastId == null)
        {
            // ‎لا تفعيلات أصلاً — نُنشئ صفّ تجريبي. الـ Code فريد لكي لا يصطدم
            // ‎مع unique index.
            var code = $"TEST-{Guid.NewGuid():N}".Substring(0, 32).ToUpperInvariant();
            await using var ins = cn.CreateCommand();
            ins.CommandText = @"
INSERT INTO [licensing].[LicenseActivations]
    ([Code],[CompanyKey],[Days],[StartDate],[EndDate],[AppliedAt],[AppliedBy],[Source],[Note])
OUTPUT INSERTED.Id
VALUES (@c, @ck, 0, @s, @e, SYSUTCDATETIME(), @u, N'Test', @n);";
            ins.Parameters.AddWithValue("@c",  code);
            ins.Parameters.AddWithValue("@ck", cfg.CompanyKey);
            ins.Parameters.AddWithValue("@s",  newEndUtc.AddDays(-1));
            ins.Parameters.AddWithValue("@e",  newEndUtc);
            ins.Parameters.AddWithValue("@u",  (object?)userId ?? DBNull.Value);
            ins.Parameters.AddWithValue("@n",  $"إدراج تجريبي بواسطة {mode}");
            lastId = (int)(await ins.ExecuteScalarAsync(ct))!;
        }
        else
        {
            await using var upd = cn.CreateCommand();
            upd.CommandText = @"
UPDATE [licensing].[LicenseActivations]
   SET EndDate = @e,
       Note    = ISNULL(Note + N' | ', N'') + @n
 WHERE Id = @id;";
            upd.Parameters.AddWithValue("@e",  newEndUtc);
            upd.Parameters.AddWithValue("@id", lastId.Value);
            upd.Parameters.AddWithValue("@n",  $"{mode}@{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
            await upd.ExecuteNonQueryAsync(ct);
        }

        // ‎أعد توقيع السلسلة كاملةً ابتداءً من الجذر، لأن أي تعديل لـ EndDate
        // ‎في صفّ يكسر تواقيع كل الصفوف اللاحقة.
        await ResignAllRowsAsync(cn, cfg.AuthKey, ct);
        return Result.Success();
    }

    /// <summary>
    /// يُعيد حساب <c>RowSig</c> لكل الصفوف بالترتيب التصاعدي. يُستخدم بعد عمليات
    /// الاختبار التي تُعدّل <c>EndDate</c> مباشرة.
    /// </summary>
    private static async Task ResignAllRowsAsync(SqlConnection cn, string authKey, CancellationToken ct)
    {
        var rows = new List<(int Id, string Code, int Days, DateTime Start, DateTime End, DateTime AppliedAt)>();
        await using (var read = cn.CreateCommand())
        {
            read.CommandText = @"
SELECT Id, Code, Days, StartDate, EndDate, AppliedAt
FROM [licensing].[LicenseActivations]
ORDER BY Id ASC;";
            await using var r = await read.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                rows.Add((
                    r.GetInt32(0),
                    r.GetString(1),
                    r.GetInt32(2),
                    DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc),
                    DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc),
                    DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc)));
            }
        }

        var prev = LicenseChainSigner.Genesis;
        foreach (var row in rows)
        {
            var sig = LicenseChainSigner.ComputeRowSig(
                authKey, row.Code, row.Days, row.Start, row.End, row.AppliedAt, prev);
            await using var upd = cn.CreateCommand();
            upd.CommandText = "UPDATE [licensing].[LicenseActivations] SET RowSig = @s WHERE Id = @id;";
            upd.Parameters.AddWithValue("@s",  sig);
            upd.Parameters.AddWithValue("@id", row.Id);
            await upd.ExecuteNonQueryAsync(ct);
            prev = sig;
        }
    }
}
