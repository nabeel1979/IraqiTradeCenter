using Microsoft.Data.SqlClient;
using IraqiTradeCenterCompany.SharedKernel.Models;

namespace IraqiTradeCenterCompany.API.Licensing;

public sealed class WalletStatus
{
    public decimal Balance  { get; init; }
    public string  Currency { get; init; } = "IQD";
}

public sealed class WalletTxnRow
{
    public int      Id        { get; set; }
    public decimal  Delta     { get; set; }
    public decimal  Balance   { get; set; }
    public string   Reason    { get; set; } = "";
    public string?  RefId     { get; set; }
    public string?  Note      { get; set; }
    public DateTime CreatedAt { get; set; }
    public string?  CreatedBy { get; set; }
}

public interface IWalletService
{
    Task<WalletStatus>       GetAsync(CancellationToken ct);
    Task<List<WalletTxnRow>> GetTransactionsAsync(int take, CancellationToken ct);
    Task<Result<decimal>>    TopupAsync(decimal amount, string reason, string? refId, string? note, string? userId, CancellationToken ct);
    Task<Result<decimal>>    ChargeAsync(decimal amount, string reason, string? refId, string? note, string? userId, CancellationToken ct);
}

public class WalletService : IWalletService
{
    private readonly IConfiguration _cfg;
    public WalletService(IConfiguration cfg) { _cfg = cfg; }

    private SqlConnection Open()
    {
        var cs = _cfg.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DefaultConnection");
        var cn = new SqlConnection(cs);
        cn.Open();
        return cn;
    }

    public async Task<WalletStatus> GetAsync(CancellationToken ct)
    {
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 Balance, Currency FROM [licensing].[Wallet] WHERE Id = 1;";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
            return new WalletStatus { Balance = 0, Currency = "IQD" };
        return new WalletStatus
        {
            Balance  = r.GetDecimal(0),
            Currency = r.GetString(1),
        };
    }

    public async Task<List<WalletTxnRow>> GetTransactionsAsync(int take, CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        await using var cn = Open();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $@"
SELECT TOP {take} Id, Delta, Balance, Reason, RefId, Note, CreatedAt, CreatedBy
FROM [licensing].[WalletTransactions]
ORDER BY Id DESC;";
        var rows = new List<WalletTxnRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            rows.Add(new WalletTxnRow
            {
                Id        = r.GetInt32(0),
                Delta     = r.GetDecimal(1),
                Balance   = r.GetDecimal(2),
                Reason    = r.GetString(3),
                RefId     = r.IsDBNull(4) ? null : r.GetString(4),
                Note      = r.IsDBNull(5) ? null : r.GetString(5),
                CreatedAt = DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc),
                CreatedBy = r.IsDBNull(7) ? null : r.GetString(7),
            });
        }
        return rows;
    }

    public Task<Result<decimal>> TopupAsync(decimal amount, string reason, string? refId, string? note, string? userId, CancellationToken ct)
        => ApplyDeltaAsync(+amount, reason, refId, note, userId, ct, requirePositive: true);

    public Task<Result<decimal>> ChargeAsync(decimal amount, string reason, string? refId, string? note, string? userId, CancellationToken ct)
        => ApplyDeltaAsync(-Math.Abs(amount), reason, refId, note, userId, ct, requirePositive: true);

    /// <summary>
    /// إضافة/خصم من المحفظة في معاملة واحدة مع تسجيل الحركة. يستخدم
    /// <c>UPDATE … WITH (UPDLOCK)</c> لمنع السباق.
    /// </summary>
    private async Task<Result<decimal>> ApplyDeltaAsync(
        decimal delta, string reason, string? refId, string? note, string? userId,
        CancellationToken ct, bool requirePositive)
    {
        if (requirePositive && delta == 0)
            return Result.Failure<decimal>("المبلغ لا يمكن أن يكون صفراً.");

        await using var cn = Open();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);
        try
        {
            decimal newBalance;
            await using (var get = cn.CreateCommand())
            {
                get.Transaction = tx;
                get.CommandText = "SELECT Balance FROM [licensing].[Wallet] WITH (UPDLOCK) WHERE Id = 1;";
                var cur = await get.ExecuteScalarAsync(ct);
                var current = (decimal)(cur ?? 0m);
                newBalance = current + delta;
                if (newBalance < 0)
                {
                    await tx.RollbackAsync(ct);
                    return Result.Failure<decimal>($"رصيد المحفظة غير كافٍ. الرصيد الحالي: {current:N3}");
                }
            }
            await using (var upd = cn.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = "UPDATE [licensing].[Wallet] SET Balance = @b, UpdatedAt = SYSUTCDATETIME() WHERE Id = 1;";
                upd.Parameters.AddWithValue("@b", newBalance);
                await upd.ExecuteNonQueryAsync(ct);
            }
            await using (var ins = cn.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"
INSERT INTO [licensing].[WalletTransactions]([Delta],[Balance],[Reason],[RefId],[Note],[CreatedBy])
VALUES (@d, @b, @r, @ref, @n, @u);";
                ins.Parameters.AddWithValue("@d",   delta);
                ins.Parameters.AddWithValue("@b",   newBalance);
                ins.Parameters.AddWithValue("@r",   reason);
                ins.Parameters.AddWithValue("@ref", (object?)refId ?? DBNull.Value);
                ins.Parameters.AddWithValue("@n",   (object?)note  ?? DBNull.Value);
                ins.Parameters.AddWithValue("@u",   (object?)userId ?? DBNull.Value);
                await ins.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Result.Success(newBalance);
        }
        catch
        {
            try { await tx.RollbackAsync(ct); } catch { }
            throw;
        }
    }
}
