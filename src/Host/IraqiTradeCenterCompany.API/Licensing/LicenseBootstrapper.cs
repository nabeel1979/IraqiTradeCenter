using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Licensing;

/// <summary>
/// يُنشئ جداول الترخيص في مخطّط <c>licensing</c> داخل قاعدة الشركة عند بدء التطبيق،
/// ويزامِن <see cref="LicenseConfig.CompanyKey"/> و<see cref="LicenseConfig.AuthKey"/>
/// مع سجلّ الشركة في قاعدة <c>IraqiTradeCenter_Parent</c> (جدول <c>T_Subscribers</c>):
/// إن وُجد سجلّ بـ <c>DatabaseName</c> مطابق لقاعدة الشركة → ننسخ AuthKey منه؛
/// وإلّا نُنشئه هناك بمفتاح عشوائي ثم نزرعه محلياً.
///
/// نتجنّب إضافة DbContext جديد كي لا نُلزم بـ migrations منفصلة — نستعمل
/// <c>IF NOT EXISTS</c> على T-SQL خام (مثالية لأن البِنية صغيرة وثابتة).
///
/// ملاحظة: لا نستخدم schema <c>sys</c> لأنه schema نظام محجوز في SQL Server
/// ولا يسمح بإنشاء جداول مستخدم فيه.
/// </summary>
public static class LicenseBootstrapper
{
    private const string Schema = "licensing";

    public static async Task EnsureCreatedAsync(IServiceProvider sp, IConfiguration cfg, CancellationToken ct = default)
    {
        var connStr = cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection");

        await using var cn = new SqlConnection(connStr);
        await cn.OpenAsync(ct);

        await ExecAsync(cn, $@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{Schema}')
    EXEC('CREATE SCHEMA [{Schema}]');", ct);

        await ExecAsync(cn, $@"
IF OBJECT_ID(N'[{Schema}].[LicenseConfig]', N'U') IS NULL
BEGIN
    CREATE TABLE [{Schema}].[LicenseConfig] (
        [Id]          INT            NOT NULL PRIMARY KEY DEFAULT 1,
        [CompanyKey]  NVARCHAR(64)   NOT NULL,
        [AuthKey]     NVARCHAR(256)  NOT NULL,
        [PricePerDay] DECIMAL(18,3)  NOT NULL DEFAULT 1000,
        [Currency]    NVARCHAR(8)    NOT NULL DEFAULT N'IQD',
        [GraceDays]   INT            NOT NULL DEFAULT 3,
        [CreatedAt]   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_LicenseConfig_Singleton CHECK ([Id] = 1)
    );
END", ct);

        await ExecAsync(cn, $@"
IF OBJECT_ID(N'[{Schema}].[LicenseActivations]', N'U') IS NULL
BEGIN
    CREATE TABLE [{Schema}].[LicenseActivations] (
        [Id]         INT            IDENTITY(1,1) PRIMARY KEY,
        [Code]       NVARCHAR(128)  NOT NULL,
        [CompanyKey] NVARCHAR(64)   NOT NULL,
        [Days]       INT            NOT NULL,
        [StartDate]  DATETIME2      NOT NULL,
        [EndDate]    DATETIME2      NOT NULL,
        [AppliedAt]  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [AppliedBy]  NVARCHAR(64)   NULL,
        [Source]     NVARCHAR(16)   NOT NULL DEFAULT N'Code',
        [Note]       NVARCHAR(500)  NULL,
        [RowSig]     NVARCHAR(64)   NULL  -- ‎توقيع الصفّ في سلسلة التواقيع (Hash Chain)
    );
    CREATE UNIQUE INDEX UX_LicenseActivations_Code
        ON [{Schema}].[LicenseActivations]([Code]);
END
ELSE IF COL_LENGTH(N'[{Schema}].[LicenseActivations]', 'RowSig') IS NULL
BEGIN
    -- ‎ترقية للنُسخ الأقدم: إضافة عمود التوقيع (يبقى NULL لحين توقيع الصفوف الموجودة).
    ALTER TABLE [{Schema}].[LicenseActivations] ADD [RowSig] NVARCHAR(64) NULL;
END", ct);

        await ExecAsync(cn, $@"
IF OBJECT_ID(N'[{Schema}].[Wallet]', N'U') IS NULL
BEGIN
    CREATE TABLE [{Schema}].[Wallet] (
        [Id]        INT            NOT NULL PRIMARY KEY DEFAULT 1,
        [Balance]   DECIMAL(18,3)  NOT NULL DEFAULT 0,
        [Currency]  NVARCHAR(8)    NOT NULL DEFAULT N'IQD',
        [UpdatedAt] DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_Wallet_Singleton CHECK ([Id] = 1)
    );
END", ct);

        await ExecAsync(cn, $@"
IF OBJECT_ID(N'[{Schema}].[WalletTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [{Schema}].[WalletTransactions] (
        [Id]        INT            IDENTITY(1,1) PRIMARY KEY,
        [Delta]     DECIMAL(18,3)  NOT NULL,
        [Balance]   DECIMAL(18,3)  NOT NULL,
        [Reason]    NVARCHAR(32)   NOT NULL,    -- 'Topup' / 'PayLicense' / 'Refund' / 'Adjustment'
        [RefId]     NVARCHAR(64)   NULL,        -- e.g. ActivationId or PaymentRequestId
        [Note]      NVARCHAR(500)  NULL,
        [CreatedAt] DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy] NVARCHAR(64)   NULL
    );
END", ct);

        await ExecAsync(cn, $@"
IF OBJECT_ID(N'[{Schema}].[PaymentRequests]', N'U') IS NULL
BEGIN
    CREATE TABLE [{Schema}].[PaymentRequests] (
        [Id]         INT            IDENTITY(1,1) PRIMARY KEY,
        [Method]     NVARCHAR(16)   NOT NULL,   -- 'Card' / 'Wallet'
        [Amount]     DECIMAL(18,3)  NOT NULL,
        [Currency]   NVARCHAR(8)    NOT NULL DEFAULT N'IQD',
        [Days]       INT            NOT NULL,
        [Status]     NVARCHAR(16)   NOT NULL DEFAULT N'Pending', -- Pending/Approved/Rejected
        [Reference]  NVARCHAR(64)   NULL,
        [Note]       NVARCHAR(500)  NULL,
        [CreatedAt]  DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
        [CreatedBy]  NVARCHAR(64)   NULL,
        [ResolvedAt] DATETIME2      NULL,
        [ResolvedBy] NVARCHAR(64)   NULL
    );
END", ct);

        // ‎زرع صفّ الإعدادات إن لم يوجد — نزامن AuthKey/CompanyKey مع Parent.T_Subscribers
        var hasConfig = await ScalarAsync<int>(cn,
            $"SELECT COUNT(*) FROM [{Schema}].[LicenseConfig]", ct);
        if (hasConfig == 0)
        {
            var dbName = cn.Database;
            var (companyKey, authKey) = await SyncWithParentAsync(cfg, dbName, ct);
            await using var ins = cn.CreateCommand();
            ins.CommandText = $@"
INSERT INTO [{Schema}].[LicenseConfig]([Id],[CompanyKey],[AuthKey],[PricePerDay],[Currency],[GraceDays])
VALUES (1, @ck, @ak, 1000, N'IQD', 3);";
            ins.Parameters.Add(new SqlParameter("@ck", companyKey));
            ins.Parameters.Add(new SqlParameter("@ak", authKey));
            await ins.ExecuteNonQueryAsync(ct);
        }

        var hasWallet = await ScalarAsync<int>(cn,
            $"SELECT COUNT(*) FROM [{Schema}].[Wallet]", ct);
        if (hasWallet == 0)
        {
            await ExecAsync(cn,
                $"INSERT INTO [{Schema}].[Wallet]([Id],[Balance],[Currency]) VALUES (1, 0, N'IQD');", ct);
        }

        // ‎توقيع الصفوف الموجودة في سلسلة التواقيع (Hash Chain) إن لم تكن موقَّعة بعد.
        // ‎هذا "checkpoint": القيم المخزَّنة الآن تُعتبر شرعية، والحماية تبدأ من هنا.
        await BackfillRowSignaturesAsync(cn, ct);
    }

    /// <summary>
    /// عند ترقية النظام (إضافة عمود <c>RowSig</c>): نوقّع الصفوف الموجودة بالترتيب
    /// التسلسلي. هذا يلتقط الحالة الحالية كنقطة مرجعية — أي تعديل لاحق على
    /// <c>EndDate</c> أو الحقول الأخرى سيكسر السلسلة ويُكتشف.
    /// </summary>
    private static async Task BackfillRowSignaturesAsync(SqlConnection cn, CancellationToken ct)
    {
        // ‎اقرأ AuthKey من LicenseConfig (لازم للتوقيع).
        string? authKey = null;
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $"SELECT TOP 1 AuthKey FROM [{Schema}].[LicenseConfig] WHERE Id = 1;";
            var o = await cmd.ExecuteScalarAsync(ct);
            if (o != null && o != DBNull.Value) authKey = (string)o;
        }
        if (string.IsNullOrEmpty(authKey)) return; // ‎لا config → لا توقيعات.

        // ‎اقرأ كل الصفوف بالترتيب التصاعدي.
        var rows = new List<(int Id, string Code, int Days, DateTime Start, DateTime End, DateTime AppliedAt, string? Sig)>();
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = $@"
SELECT Id, Code, Days, StartDate, EndDate, AppliedAt, RowSig
FROM [{Schema}].[LicenseActivations]
ORDER BY Id ASC;";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                rows.Add((
                    r.GetInt32(0),
                    r.GetString(1),
                    r.GetInt32(2),
                    DateTime.SpecifyKind(r.GetDateTime(3), DateTimeKind.Utc),
                    DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc),
                    DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc),
                    r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }

        // ‎احسب التوقيع المتسلسل وحدِّث الصفوف التي توقيعها NULL فقط.
        var prev = LicenseChainSigner.Genesis;
        foreach (var row in rows)
        {
            var expected = LicenseChainSigner.ComputeRowSig(
                authKey, row.Code, row.Days, row.Start, row.End, row.AppliedAt, prev);

            if (string.IsNullOrEmpty(row.Sig))
            {
                await using var upd = cn.CreateCommand();
                upd.CommandText = $@"UPDATE [{Schema}].[LicenseActivations] SET RowSig = @s WHERE Id = @id;";
                upd.Parameters.AddWithValue("@s",  expected);
                upd.Parameters.AddWithValue("@id", row.Id);
                await upd.ExecuteNonQueryAsync(ct);
                prev = expected;
            }
            else
            {
                // ‎الصفّ موقَّع مسبقاً — نستخدم القيمة المخزّنة كـ prev للصفّ التالي.
                // ‎لو القيمة المخزَّنة لا تطابق المتوقَّع، فالصفّ مزوَّر — لكن لا نُصلحه
                // ‎هنا، ندعه يُكتشف وقت <c>GetStatusAsync</c>.
                prev = row.Sig!;
            }
        }
    }

    /// <summary>
    /// يبحث عن سجلّ الشركة في <c>IraqiTradeCenter_Parent.dbo.T_Subscribers</c>
    /// بمطابقة <c>DatabaseName</c>. إن وُجد: يأخذ AuthKey + AuthKey-as-CompanyKey منه.
    /// إن لم يوجد ولم تتوفّر سلسلة اتصال Parent: يولّد محلياً (fallback).
    /// </summary>
    private static async Task<(string CompanyKey, string AuthKey)> SyncWithParentAsync(
        IConfiguration cfg, string dbName, CancellationToken ct)
    {
        var parentCs = cfg.GetConnectionString("ParentConnection");
        if (string.IsNullOrWhiteSpace(parentCs))
            return (DeriveCompanyKey(dbName), GenerateAuthKey());

        try
        {
            await using var pcn = new SqlConnection(parentCs);
            await pcn.OpenAsync(ct);

            // ابحث عن المشترك بـ DatabaseName
            string? existingAuthKey = null;
            await using (var get = pcn.CreateCommand())
            {
                get.CommandText = "SELECT TOP 1 AuthKey FROM dbo.T_Subscribers WHERE DatabaseName = @db";
                get.Parameters.AddWithValue("@db", dbName);
                var o = await get.ExecuteScalarAsync(ct);
                if (o != null && o != DBNull.Value) existingAuthKey = (string)o;
            }

            if (!string.IsNullOrWhiteSpace(existingAuthKey))
                return (DeriveCompanyKey(dbName), existingAuthKey!);

            // غير موجود → أنشئه
            var newCompanyKey = DeriveCompanyKey(dbName);
            var newAuthKey = GenerateAuthKey();
            await using (var ins = pcn.CreateCommand())
            {
                ins.CommandText = @"
INSERT INTO dbo.T_Subscribers (Dscrp, DatabaseName, AuthKey, StartDate, EndDate, Active, Adress, CommissionRate)
VALUES (@d, @db, @ak, CONVERT(VARCHAR(10), GETDATE(), 23),
        CONVERT(VARCHAR(10), DATEADD(DAY, 30, GETDATE()), 23),
        1, N'', 5);";
                ins.Parameters.AddWithValue("@d",  $"شركة {newCompanyKey}");
                ins.Parameters.AddWithValue("@db", dbName);
                ins.Parameters.AddWithValue("@ak", newAuthKey);
                await ins.ExecuteNonQueryAsync(ct);
            }
            return (newCompanyKey, newAuthKey);
        }
        catch
        {
            // ‎في حال تعذّر الاتصال بـ Parent (مثلاً في تطوير محلي): توليد محلي
            return (DeriveCompanyKey(dbName), GenerateAuthKey());
        }
    }

    private static async Task ExecAsync(SqlConnection cn, string sql, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<T> ScalarAsync<T>(SqlConnection cn, string sql, CancellationToken ct)
    {
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        var o = await cmd.ExecuteScalarAsync(ct);
        return (T)Convert.ChangeType(o!, typeof(T));
    }

    /// <summary>
    /// يستخرج كود الشركة من اسم قاعدة بياناتها: مثلاً <c>IraqiTradeCenter_Company_001</c>
    /// يصبح <c>C001</c>. لو الاسم لا يتطابق مع النمط، نأخذ آخر مقطع رقمي/حرفي.
    /// </summary>
    private static string DeriveCompanyKey(string dbName)
    {
        var parts = dbName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var last = parts.LastOrDefault() ?? "DEF";
        // نضع البادئة C ليتميّز الكود ويسهل قراءته
        return $"C{last.ToUpperInvariant()}";
    }

    /// <summary>
    /// مفتاح سرّي عشوائي 32 بايت (256 بت) بترميز base64 — يُستخدم لتوقيع الشفرة.
    /// </summary>
    private static string GenerateAuthKey()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
