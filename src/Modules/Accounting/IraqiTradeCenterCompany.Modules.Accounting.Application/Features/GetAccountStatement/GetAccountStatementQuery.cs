using System.Text.Json;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountStatement;

/// <summary>
/// كشف حساب: التاريخ - مدين - دائن - الرصيد - رصيد مقوم - البيان (مقاييس بالعملة الأساسية للتجميع التقريري)
/// </summary>
public record GetAccountStatementQuery(
    DateTime From,
    DateTime To,
    int? AccountId = null,
    string? Currency = null,
    bool IncludeDraft = false,
    string? BaseCurrency = null,
    string? ExchangeRatesJson = null
) : IRequest<AccountStatementDto>;

public class GetAccountStatementHandler : IRequestHandler<GetAccountStatementQuery, AccountStatementDto>
{
    private readonly IAccountingDbContext _db;
    public GetAccountStatementHandler(IAccountingDbContext db) => _db = db;

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static Dictionary<string, decimal> ParseRates(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var r = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (d == null) return r;
            foreach (var kv in d)
            {
                if (kv.Value > 0)
                    r[kv.Key.Trim().ToUpperInvariant()] = kv.Value;
            }
            return r;
        }
        catch
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// يحسب مُضاعِف التحويل من عملة السطر إلى العملة الأساسية:
    ///  - Operation=Multiply (1): BaseAmount = ForeignAmount × Rate  →  multiplier = Rate
    ///  - Operation=Divide   (2): BaseAmount = ForeignAmount ÷ Rate  →  multiplier = 1/Rate
    /// إذا كانت العملة هي الأساسية أو غير مذكورة، الـ multiplier = 1.
    /// </summary>
    private static decimal GetMultiplier(
        string lineCurrency,
        string baseCurrency,
        IReadOnlyDictionary<string, (decimal Rate, int Operation)> rates,
        ref bool usedFallback)
    {
        var c = string.IsNullOrWhiteSpace(lineCurrency) ? baseCurrency.Trim().ToUpperInvariant() : lineCurrency.Trim().ToUpperInvariant();
        var b = baseCurrency.Trim().ToUpperInvariant();
        if (c == b)
            return 1m;
        if (rates.TryGetValue(c, out var entry) && entry.Rate > 0)
        {
            // 2 = Divide
            if (entry.Operation == 2) return 1m / entry.Rate;
            // 1 = Multiply (الافتراضي)
            return entry.Rate;
        }
        usedFallback = true;
        return 1m;
    }

    public async Task<AccountStatementDto> Handle(GetAccountStatementQuery req, CancellationToken ct)
    {
        var fromDate = req.From.Date;
        var toDate = req.To.Date.AddDays(1).AddTicks(-1);

        var allowedStatuses = req.IncludeDraft
            ? new[] { JournalEntryStatus.Posted, JournalEntryStatus.Draft }
            : new[] { JournalEntryStatus.Posted };

        var currency = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim().ToUpperInvariant();

        // ───────────────────────────────────────────────────────────
        // السنة المالية المرجعية لاحتساب رصيد الأرباح/الخسائر:
        //   حسابات Revenue/Expense (4،5) لا تُرَحَّل أرصدتها بين السنوات،
        //   فيُقيَّد احتساب الافتتاحي وحركة الفترة لها بـ EntryDate >= @plFyStart.
        //   الأولوية للسنة المُعَلَّمة كنشطة (IsActive=true)، ثم التي تحتوي @from.
        // ───────────────────────────────────────────────────────────
        DateTime? plFyStart = null;
        var containingFy = await _db.FiscalYears.AsNoTracking()
            .Where(f => f.StartDate <= fromDate && f.EndDate >= fromDate)
            .OrderByDescending(f => f.StartDate)
            .FirstOrDefaultAsync(ct);
        if (containingFy != null)
        {
            plFyStart = containingFy.StartDate.Date;
        }
        else
        {
            var activeFy = await _db.FiscalYears.AsNoTracking()
                .FirstOrDefaultAsync(f => f.IsActive, ct);
            if (activeFy != null) plFyStart = activeFy.StartDate.Date;
        }

        // ─────────────────────────────────────────
        // المصدر الأساسي لأسعار الصرف: أحدث نشرة منشورة سارية على تاريخ نهاية الفترة.
        // إن لم توجد نشرة، نستخدم BaseCurrency + ExchangeRatesJson كسلوك توافقي (legacy).
        // ─────────────────────────────────────────
        var bulletin = await _db.CurrencyRateBulletins
            .Include(b => b.Lines)
            .Where(b => b.Status == CurrencyRateBulletinStatus.Published && b.EffectiveAt <= toDate)
            .OrderByDescending(b => b.EffectiveAt).ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

        string baseCur;
        Dictionary<string, (decimal Rate, int Operation)> rates;
        if (bulletin != null)
        {
            baseCur = (bulletin.BaseCurrency ?? "IQD").Trim().ToUpperInvariant();
            rates = bulletin.Lines
                .Where(l => l.Rate > 0 && !string.IsNullOrWhiteSpace(l.Currency))
                .ToDictionary(
                    l => l.Currency.Trim().ToUpperInvariant(),
                    l => (l.Rate, (int)l.Operation),
                    StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            baseCur = string.IsNullOrWhiteSpace(req.BaseCurrency) ? "IQD" : req.BaseCurrency.Trim().ToUpperInvariant();
            // الـ JSON القديم: قِيَم بسيطة (Rate فقط)، نعتبرها Multiply افتراضياً
            var legacy = ParseRates(req.ExchangeRatesJson);
            rates = legacy.ToDictionary(
                kv => kv.Key,
                kv => (kv.Value, 1),
                StringComparer.OrdinalIgnoreCase);
        }

        var statusInts = allowedStatuses.Select(s => (int)s).ToArray();
        bool fxFallback = false;

        // الحركات داخل الفترة: النوع Normal فقط — قيود Opening تُعامَل كرصيد افتتاحي ولا تظهر بين الحركات.
        // ملاحظة: حسابات الأرباح/الخسائر (4،5) نقصر حركتها على السنة المالية المرجعية حتى لو
        //         امتدّ مدى التقرير لسنوات سابقة، لأن قيود تلك السنوات تخصّ نتيجة سنة منفصلة.
        var inPeriodSql = @"
SELECT 
    l.Id            AS LineId,
    e.Id            AS EntryId,
    e.EntryDate     AS EntryDate,
    e.EntryNumber   AS EntryNumber,
    e.[Description] AS EntryDescription,
    e.Currency      AS EntryCurrency,
    l.AccountId     AS AccountId,
    CAST(l.IsDebit AS INT) AS IsDebitInt,
    l.Amount        AS Amount,
    l.[Description] AS LineDescription,
    e.EntryType     AS EntryType,
    e.Source        AS Source,
    e.ReferenceType AS ReferenceType,
    e.ReferenceId   AS ReferenceId,
    e.ReferenceNumber AS ReferenceNumber,
    vt.Code         AS VoucherTypeCode,
    e.VoucherSequence AS VoucherSequence
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
LEFT JOIN acc.JournalVoucherTypes vt ON vt.Id = e.VoucherTypeId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND e.EntryType = 1
  AND e.EntryDate >= @from AND e.EntryDate <= @to
  AND (a.[Type] IN (1,2,3)
       OR (a.[Type] IN (4,5)
            AND (@plFyStart IS NULL OR e.EntryDate >= @plFyStart)))
  AND (@accountId IS NULL OR l.AccountId = @accountId)
  AND (@currency IS NULL OR e.Currency = @currency)
ORDER BY e.EntryDate, e.Id, l.Id;";

        var conn = _db.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var rawList = new List<(int LineId, int EntryId, DateTime EntryDate, string EntryNumber, string EntryDesc,
            string EntryCurrency, int AccountId, bool IsDebit, decimal Amount, string? LineDesc,
            int EntryTypeInt, int SourceInt, string? RefType, int? RefId, string? RefNumber,
            string? VoucherTypeCode, int? VoucherSequence)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = inPeriodSql;
            AddParam(cmd, "@from", fromDate);
            AddParam(cmd, "@to", toDate);
            AddParam(cmd, "@accountId", (object?)req.AccountId ?? DBNull.Value);
            AddParam(cmd, "@currency", (object?)currency ?? DBNull.Value);
            AddParam(cmd, "@plFyStart", (object?)plFyStart ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                rawList.Add((
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetDateTime(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetInt32(6),
                    reader.GetInt32(7) == 1,
                    reader.GetDecimal(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.GetInt32(10),
                    reader.GetInt32(11),
                    reader.IsDBNull(12) ? null : reader.GetString(12),
                    reader.IsDBNull(13) ? null : reader.GetInt32(13),
                    reader.IsDBNull(14) ? null : reader.GetString(14),
                    reader.IsDBNull(15) ? null : reader.GetString(15),
                    reader.IsDBNull(16) ? null : reader.GetInt32(16)
                ));
            }
        }

        // الرصيد الافتتاحي:
        //   • أصول/خصوم/حقوق ملكية: حركات Normal قبل @from + قيود Opening حتى @to.
        //   • أرباح/خسائر: حركات Normal من بداية السنة المالية المرجعية حتى @from
        //                  (لا تُحسب قيود Opening، ولا حركات السنوات السابقة).
        decimal openingBalance = 0m;
        var openingSql = @"
SELECT 
    ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
    ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND (
        (a.[Type] IN (1,2,3) AND (
            (e.EntryType = 1 AND e.EntryDate < @from)
         OR (e.EntryType = 2 AND e.EntryDate <= @to)
        ))
     OR (a.[Type] IN (4,5) AND e.EntryType = 1 AND e.EntryDate < @from
            AND (@plFyStart IS NULL OR e.EntryDate >= @plFyStart))
      )
  AND (@accountId IS NULL OR l.AccountId = @accountId)
  AND (@currency IS NULL OR e.Currency = @currency);";

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = openingSql;
            AddParam(cmd, "@from", fromDate);
            AddParam(cmd, "@to", toDate);
            AddParam(cmd, "@accountId", (object?)req.AccountId ?? DBNull.Value);
            AddParam(cmd, "@currency", (object?)currency ?? DBNull.Value);
            AddParam(cmd, "@plFyStart", (object?)plFyStart ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                openingBalance = reader.GetDecimal(0) - reader.GetDecimal(1);
            }
        }

        // رصيد افتتاحي مقوَّم بالعملة الأساسية — لكل عملة على حدة
        decimal openingValuated = 0m;
        var openingByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var openingByCcySql = @"
SELECT e.Currency AS Ccy,
  ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
  ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND (
        (a.[Type] IN (1,2,3) AND (
            (e.EntryType = 1 AND e.EntryDate < @from)
         OR (e.EntryType = 2 AND e.EntryDate <= @to)
        ))
     OR (a.[Type] IN (4,5) AND e.EntryType = 1 AND e.EntryDate < @from
            AND (@plFyStart IS NULL OR e.EntryDate >= @plFyStart))
      )
  AND (@accountId IS NULL OR l.AccountId = @accountId)
  AND (@currency IS NULL OR e.Currency = @currency)
GROUP BY e.Currency;";

        await using (var cmd2 = conn.CreateCommand())
        {
            cmd2.CommandText = openingByCcySql;
            AddParam(cmd2, "@from", fromDate);
            AddParam(cmd2, "@to", toDate);
            AddParam(cmd2, "@accountId", (object?)req.AccountId ?? DBNull.Value);
            AddParam(cmd2, "@currency", (object?)currency ?? DBNull.Value);
            AddParam(cmd2, "@plFyStart", (object?)plFyStart ?? DBNull.Value);
            await using var reader = await cmd2.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var ccy = reader.GetString(0);
                var net = reader.GetDecimal(1) - reader.GetDecimal(2);
                var mult = GetMultiplier(ccy, baseCur, rates, ref fxFallback);
                openingValuated += net * mult;
                var key = (ccy ?? string.Empty).Trim().ToUpperInvariant();
                if (!string.IsNullOrEmpty(key))
                    openingByCurrency[key] = (openingByCurrency.TryGetValue(key, out var prev) ? prev : 0m) + net;
            }
        }

        // ── تفاصيل قيود الافتتاح (EntryType=2) لعرضها في رأس الكشف.
        // يجمع السطور المتعلقة بالحساب ضمن نفس قيد الافتتاح في صف واحد ليبقى
        // الكشف مفهوماً، ويعرض المبالغ بعملة القيد + المقوَّم بالعملة الأساسية.
        var openingEntries = new List<OpeningEntryRowDto>();
        // قيود الافتتاح تخصّ أساساً الحسابات الميزانية (1،2،3). نُقصّر الاستعلام عليها
        // ليتسق العرض مع احتساب الافتتاحي.
        var openingEntriesSql = @"
SELECT
    e.Id              AS EntryId,
    e.EntryNumber     AS EntryNumber,
    e.EntryDate       AS EntryDate,
    e.Currency        AS Currency,
    e.[Description]   AS EntryDescription,
    ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Debit,
    ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Credit
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND e.EntryType = 2
  AND e.EntryDate <= @to
  AND a.[Type] IN (1,2,3)
  AND (@accountId IS NULL OR l.AccountId = @accountId)
  AND (@currency IS NULL OR e.Currency = @currency)
GROUP BY e.Id, e.EntryNumber, e.EntryDate, e.Currency, e.[Description]
ORDER BY e.EntryDate, e.EntryNumber;";
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = openingEntriesSql;
            AddParam(cmd, "@to", toDate);
            AddParam(cmd, "@accountId", (object?)req.AccountId ?? DBNull.Value);
            AddParam(cmd, "@currency", (object?)currency ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var ccy = reader["Currency"]?.ToString() ?? baseCur;
                var debit = reader.GetDecimal(reader.GetOrdinal("Debit"));
                var credit = reader.GetDecimal(reader.GetOrdinal("Credit"));
                var net = debit - credit;
                var mult = GetMultiplier(ccy, baseCur, rates, ref fxFallback);
                openingEntries.Add(new OpeningEntryRowDto
                {
                    EntryId = reader.GetInt32(reader.GetOrdinal("EntryId")),
                    EntryNumber = reader["EntryNumber"]?.ToString() ?? "",
                    EntryDate = reader.GetDateTime(reader.GetOrdinal("EntryDate")),
                    Currency = ccy,
                    Description = reader["EntryDescription"]?.ToString(),
                    Debit = debit,
                    Credit = credit,
                    Net = net,
                    NetValuated = net * mult,
                });
            }
        }

        // قاموس مُضاعِفات التحويل لكل عملة موجودة في الكشف،
        // ليستخدمه الـ frontend في عرض "الرصيد المقوّم" داخل جداول العملات الفردية.
        var multipliersByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var ccy in rawList.Select(r => r.EntryCurrency).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var key = (ccy ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(key)) continue;
            multipliersByCurrency[key] = GetMultiplier(key, baseCur, rates, ref fxFallback);
        }
        foreach (var ccy in openingByCurrency.Keys)
        {
            if (!multipliersByCurrency.ContainsKey(ccy))
                multipliersByCurrency[ccy] = GetMultiplier(ccy, baseCur, rates, ref fxFallback);
        }

        var lines = rawList.Select(r => new
        {
            r.LineId,
            r.EntryId,
            r.EntryDate,
            r.EntryNumber,
            r.EntryDesc,
            r.EntryCurrency,
            r.AccountId,
            r.IsDebit,
            r.Amount,
            r.LineDesc,
            r.EntryTypeInt,
            r.SourceInt,
            r.RefType,
            r.RefId,
            r.RefNumber,
            r.VoucherTypeCode,
            r.VoucherSequence
        }).ToList();

        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        if (req.AccountId.HasValue && !accountIds.Contains(req.AccountId.Value))
            accountIds.Add(req.AccountId.Value);

        var accountsMap = accountIds.Count == 0
            ? new Dictionary<int, (string Code, string NameAr)>()
            : await _db.Accounts.AsNoTracking()
                .Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => (a.Code, a.NameAr), ct);

        var rows = new List<AccountStatementRowDto>(lines.Count);
        decimal balance = openingBalance;
        decimal balanceVal = openingValuated;
        decimal totalDebit = 0, totalCredit = 0;
        decimal totalDebitVal = 0, totalCreditVal = 0;

        foreach (var l in lines)
        {
            var debit = l.IsDebit ? l.Amount : 0m;
            var credit = !l.IsDebit ? l.Amount : 0m;
            balance += debit - credit;
            var mult = GetMultiplier(l.EntryCurrency, baseCur, rates, ref fxFallback);
            var debitVal = debit * mult;
            var creditVal = credit * mult;
            balanceVal += debitVal - creditVal;
            totalDebit += debit;
            totalCredit += credit;
            totalDebitVal += debitVal;
            totalCreditVal += creditVal;

            accountsMap.TryGetValue(l.AccountId, out var acc);

            rows.Add(new AccountStatementRowDto
            {
                Date = l.EntryDate,
                EntryNumber = l.EntryNumber,
                EntryId = l.EntryId,
                AccountId = l.AccountId,
                AccountCode = acc.Code ?? "",
                AccountName = acc.NameAr ?? $"#{l.AccountId}",
                Description = l.EntryDesc,
                LineDescription = l.LineDesc,
                Debit = debit,
                Credit = credit,
                Balance = balance,
                BalanceValuated = balanceVal,
                Currency = l.EntryCurrency,
                EntryType = ((JournalEntryType)l.EntryTypeInt).ToString(),
                Source = ((JournalEntrySource)l.SourceInt).ToString(),
                ReferenceType = l.RefType,
                ReferenceId = l.RefId,
                ReferenceNumber = l.RefNumber,
                VoucherTypeCode = l.VoucherTypeCode,
                VoucherSequence = l.VoucherSequence,
                VoucherNumber = (l.VoucherSequence.HasValue && !string.IsNullOrWhiteSpace(l.VoucherTypeCode))
                    ? $"{l.VoucherTypeCode}-{l.VoucherSequence.Value}"
                    : null,
            });
        }

        string? accCode = null, accName = null;
        if (req.AccountId.HasValue && accountsMap.TryGetValue(req.AccountId.Value, out var selAcc))
        {
            accCode = selAcc.Code;
            accName = selAcc.NameAr;
        }

        return new AccountStatementDto
        {
            FromDate = fromDate,
            ToDate = req.To.Date,
            AccountId = req.AccountId,
            AccountCode = accCode,
            AccountName = accName,
            Currency = currency ?? "IQD",
            BaseCurrency = baseCur,
            FxUsedFallback = fxFallback,
            FxBulletinName = bulletin?.Name,
            FxBulletinEffectiveAt = bulletin?.EffectiveAt,
            IsAllAccounts = !req.AccountId.HasValue,
            OpeningBalance = openingBalance,
            OpeningBalanceValuated = openingValuated,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = balance,
            TotalDebitValuated = totalDebitVal,
            TotalCreditValuated = totalCreditVal,
            ClosingBalanceValuated = balanceVal,
            OpeningByCurrency = openingByCurrency,
            CurrencyMultipliers = multipliersByCurrency,
            OpeningEntries = openingEntries,
            Rows = rows,
        };
    }
}
