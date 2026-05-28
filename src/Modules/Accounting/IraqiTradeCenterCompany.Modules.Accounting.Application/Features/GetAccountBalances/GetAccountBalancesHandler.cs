using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountBalances;

/// <summary>
/// يحسب رصيد كل (حساب × عملة) = الافتتاحي + حركة الفترة.
/// يُعيد الرصيد مُصنَّفاً: مدين أو دائن، مع رصيد مقوَّم اختياري.
/// </summary>
public class GetAccountBalancesHandler : IRequestHandler<GetAccountBalancesQuery, AccountBalancesDto>
{
    private readonly IAccountingDbContext _db;
    public GetAccountBalancesHandler(IAccountingDbContext db) => _db = db;

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static decimal GetMultiplier(
        string? lineCurrency, string baseCurrency,
        IReadOnlyDictionary<string, (decimal Rate, int Operation)> rates,
        ref bool usedFallback)
    {
        var b = baseCurrency.Trim().ToUpperInvariant();
        var c = string.IsNullOrWhiteSpace(lineCurrency) ? b : lineCurrency.Trim().ToUpperInvariant();
        if (c == b) return 1m;
        if (rates.TryGetValue(c, out var entry) && entry.Rate > 0)
            return entry.Operation == 2 ? 1m / entry.Rate : entry.Rate;
        usedFallback = true;
        return 1m;
    }

    public async Task<AccountBalancesDto> Handle(GetAccountBalancesQuery req, CancellationToken ct)
    {
        var fromDate = req.FromDate.Date;
        var toDate   = req.ToDate.Date.AddDays(1).AddTicks(-1);
        var currencyFilter = string.IsNullOrWhiteSpace(req.Currency)
            ? null : req.Currency.Trim().ToUpperInvariant();

        var statusInts = (req.IncludeDraft
            ? new[] { JournalEntryStatus.Posted, JournalEntryStatus.Draft }
            : new[] { JournalEntryStatus.Posted })
            .Select(s => (int)s).ToArray();

        // ── السنة المالية (لحسابات الأرباح/الخسائر)
        DateTime? plFyStart = null;
        var fy = await _db.FiscalYears.AsNoTracking()
            .Where(f => f.StartDate <= fromDate && f.EndDate >= fromDate)
            .OrderByDescending(f => f.StartDate).FirstOrDefaultAsync(ct)
            ?? await _db.FiscalYears.AsNoTracking().FirstOrDefaultAsync(f => f.IsActive, ct);
        if (fy != null) plFyStart = fy.StartDate.Date;

        // ── نشرة الأسعار للتقويم
        string baseCur = "IQD";
        string? bulletinName = null;
        DateTime? bulletinDate = null;
        var rates = new Dictionary<string, (decimal Rate, int Operation)>(StringComparer.OrdinalIgnoreCase);
        bool fxFallback = false;

        if (req.Valuated)
        {
            var bulletin = await _db.CurrencyRateBulletins
                .Include(b => b.Lines)
                .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= toDate)
                .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
                .FirstOrDefaultAsync(ct);
            if (bulletin != null)
            {
                baseCur = (bulletin.BaseCurrency ?? "IQD").Trim().ToUpperInvariant();
                bulletinName = bulletin.Name;
                bulletinDate = bulletin.EffectiveAt;
                foreach (var line in bulletin.Lines.Where(l => l.Rate > 0 && !string.IsNullOrWhiteSpace(l.Currency)))
                    rates[line.Currency.Trim().ToUpperInvariant()] = (line.Rate, (int)line.Operation);
            }
        }

        // ── جلب شجرة الحسابات
        var allAccounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.NameAr, a.Type, a.Nature, a.Level, a.IsLeaf, a.ParentId })
            .ToListAsync(ct);

        // ── فلتر بالحساب المحدد وأحفاده
        HashSet<int>? accountIdFilter = null;
        if (req.AccountId.HasValue)
        {
            accountIdFilter = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(req.AccountId.Value);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                accountIdFilter.Add(id);
                foreach (var child in allAccounts.Where(a => a.ParentId == id))
                    queue.Enqueue(child.Id);
            }
        }

        // الحسابات المرئية وفق فلاتر العرض
        var displayAccounts = allAccounts
            .Where(a => (accountIdFilter == null || accountIdFilter.Contains(a.Id))
                     && (!req.LeavesOnly || a.IsLeaf)
                     && (!req.MaxLevel.HasValue || req.MaxLevel.Value <= 0 || a.Level <= req.MaxLevel.Value))
            .ToList();

        if (displayAccounts.Count == 0)
            return new AccountBalancesDto
            {
                FromDate = fromDate, ToDate = req.ToDate.Date,
                FilterCurrency = currencyFilter, FilterAccountId = req.AccountId,
                Valuated = req.Valuated, BaseCurrency = baseCur,
                FxBulletinName = bulletinName, FxBulletinEffectiveAt = bulletinDate,
                MaxLevel = req.MaxLevel, LeavesOnly = req.LeavesOnly,
            };

        // ── SQL: الافتتاحي + حركة الفترة لكل (حساب × عملة)
        var statusStr = string.Join(",", statusInts);

        var openingSql = $@"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit=1 THEN l.Amount ELSE 0 END),0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit=0 THEN l.Amount ELSE 0 END),0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id=l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id=l.AccountId
WHERE l.IsDeleted=0 AND e.IsDeleted=0
  AND e.[Status] IN ({statusStr})
  AND (
    (a.[Type] IN (1,2,3) AND ((e.EntryType=1 AND e.EntryDate<@from) OR (e.EntryType=2 AND e.EntryDate<=@to)))
    OR
    (a.[Type] IN (4,5) AND e.EntryType=1 AND e.EntryDate<@from AND (@plFyStart IS NULL OR e.EntryDate>=@plFyStart))
  )
  AND (@currency IS NULL OR UPPER(e.Currency)=@currency)
GROUP BY l.AccountId, e.Currency;";

        var periodSql = $@"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit=1 THEN l.Amount ELSE 0 END),0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit=0 THEN l.Amount ELSE 0 END),0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id=l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id=l.AccountId
WHERE l.IsDeleted=0 AND e.IsDeleted=0
  AND e.[Status] IN ({statusStr})
  AND e.EntryType=1
  AND e.EntryDate>=@from AND e.EntryDate<=@to
  AND (a.[Type] IN (1,2,3) OR (a.[Type] IN (4,5) AND (@plFyStart IS NULL OR e.EntryDate>=@plFyStart)))
  AND (@currency IS NULL OR UPPER(e.Currency)=@currency)
GROUP BY l.AccountId, e.Currency;";

        var openingByAccCcy = new Dictionary<(int, string), (decimal Dr, decimal Cr)>();
        var periodByAccCcy  = new Dictionary<(int, string), (decimal Dr, decimal Cr)>();

        var conn = _db.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);

        async Task FillAsync(string sql, Dictionary<(int, string), (decimal, decimal)> target)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParam(cmd, "@from",      fromDate);
            AddParam(cmd, "@to",        toDate);
            AddParam(cmd, "@currency",  (object?)currencyFilter ?? DBNull.Value);
            AddParam(cmd, "@plFyStart", (object?)plFyStart ?? DBNull.Value);
            await using var rdr = await cmd.ExecuteReaderAsync(ct);
            while (await rdr.ReadAsync(ct))
            {
                var accId = rdr.GetInt32(0);
                var ccy = (rdr.IsDBNull(1) ? "" : rdr.GetString(1)).Trim().ToUpperInvariant();
                target[(accId, ccy)] = (rdr.GetDecimal(2), rdr.GetDecimal(3));
            }
        }

        await FillAsync(openingSql, openingByAccCcy);
        await FillAsync(periodSql, periodByAccCcy);

        // ── تجميع على الآباء (إذا LeavesOnly=false)
        if (!req.LeavesOnly)
        {
            var parentMap = allAccounts.ToDictionary(a => a.Id, a => a.ParentId);
            void Bubble(Dictionary<(int, string), (decimal, decimal)> src)
            {
                var leafKeys = src.ToList();
                foreach (var kv in leafKeys)
                {
                    if (!parentMap.TryGetValue(kv.Key.Item1, out var pid)) continue;
                    while (pid.HasValue)
                    {
                        var pk = (pid.Value, kv.Key.Item2);
                        var prev = src.TryGetValue(pk, out var pv) ? pv : (0m, 0m);
                        src[pk] = (prev.Item1 + kv.Value.Item1, prev.Item2 + kv.Value.Item2);
                        if (!parentMap.TryGetValue(pid.Value, out var next)) break;
                        pid = next;
                    }
                }
            }
            Bubble(openingByAccCcy);
            Bubble(periodByAccCcy);
        }

        // ── بناء الصفوف
        var rows = new List<AccountBalanceRowDto>();
        decimal totDr = 0, totCr = 0, totValDr = 0, totValCr = 0;

        // نجمع كل عملات كل حساب
        var allKeys = openingByAccCcy.Keys.Concat(periodByAccCcy.Keys).Distinct().ToList();

        foreach (var acc in displayAccounts)
        {
            var ccys = allKeys.Where(k => k.Item1 == acc.Id).Select(k => k.Item2).Distinct().ToList();
            if (ccys.Count == 0) continue; // لا حركة

            foreach (var ccy in ccys.OrderBy(c => c))
            {
                var opKey  = (acc.Id, ccy);
                var (oDr, oCr) = openingByAccCcy.TryGetValue(opKey, out var ov) ? ov : (0m, 0m);
                var (pDr, pCr) = periodByAccCcy.TryGetValue(opKey, out var pv) ? pv : (0m, 0m);

                var net = (oDr + pDr) - (oCr + pCr);
                var debit  = net > 0 ? net  : 0m;
                var credit = net < 0 ? -net : 0m;

                var mult = req.Valuated ? GetMultiplier(ccy, baseCur, rates, ref fxFallback) : 1m;
                var valDr = debit  * mult;
                var valCr = credit * mult;

                rows.Add(new AccountBalanceRowDto
                {
                    AccountId     = acc.Id,
                    AccountCode   = acc.Code,
                    AccountName   = acc.NameAr,
                    AccountType   = acc.Type.ToString(),
                    AccountNature = acc.Nature.ToString(),
                    Level         = acc.Level,
                    IsLeaf        = acc.IsLeaf,
                    ParentId      = acc.ParentId,
                    Currency      = ccy,
                    DebitBalance  = debit,
                    CreditBalance = credit,
                    ValuatedDebit = valDr,
                    ValuatedCredit= valCr,
                });

                totDr    += debit;
                totCr    += credit;
                totValDr += valDr;
                totValCr += valCr;
            }
        }

        return new AccountBalancesDto
        {
            FromDate             = fromDate,
            ToDate               = req.ToDate.Date,
            FilterCurrency       = currencyFilter,
            FilterAccountId      = req.AccountId,
            Valuated             = req.Valuated,
            BaseCurrency         = baseCur,
            FxBulletinName       = bulletinName,
            FxBulletinEffectiveAt= bulletinDate,
            FxUsedFallback       = fxFallback,
            MaxLevel             = req.MaxLevel,
            LeavesOnly           = req.LeavesOnly,
            Rows                 = rows,
            TotalDebit           = totDr,
            TotalCredit          = totCr,
            TotalValuatedDebit   = totValDr,
            TotalValuatedCredit  = totValCr,
        };
    }
}
