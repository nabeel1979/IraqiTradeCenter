using IraqiTradeCenterCompany.Modules.Accounting.Application.Dtos;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetTrialBalance;

/// <summary>
/// مُعالج ميزان المراجعة الموسَّع.
///
/// لكل حساب يُحسب:
///   1) الافتتاحي (الفترة السابقة):
///        - حركة Normal لجميع القيود قبل @from
///        - + كل قيود Opening (النوع 2) حتى @to (الافتتاحي يُحسب دائماً ضمن الافتتاحي ولا يُعرَض كحركة)
///        - يُحوَّل صافي (مدين − دائن) إلى عمودَي OpeningDebit/OpeningCredit بحسب الإشارة.
///   2) حركة الفترة الحالية (PeriodDebit/PeriodCredit):
///        - مجموع المدين والدائن لحركات Normal خلال [@from, @to].
///   3) الرصيد الختامي (ClosingDebit/ClosingCredit):
///        - افتتاحي مع جانبه + (مدين الفترة − دائن الفترة) ⇒ يُسجَّل في عموده وفق الإشارة.
///
/// التقويم بالعملة الأساسية اختياري: عند Valuated=true يُضرب كل مبلغ بمضاعِف نشرة الأسعار
/// (أحدث نشرة منشورة سارية على @to). إذا فُعّل فلتر العملة، نُحضر الحسابات بتلك العملة فقط
/// (دون تقويم).
/// </summary>
public class GetTrialBalanceHandler : IRequestHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    private readonly IAccountingDbContext _db;
    public GetTrialBalanceHandler(IAccountingDbContext db) => _db = db;

    private static void AddParam(System.Data.Common.DbCommand cmd, string name, object value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    private static decimal GetMultiplier(
        string? lineCurrency,
        string baseCurrency,
        IReadOnlyDictionary<string, (decimal Rate, int Operation)> rates,
        ref bool usedFallback)
    {
        var b = baseCurrency.Trim().ToUpperInvariant();
        var c = string.IsNullOrWhiteSpace(lineCurrency) ? b : lineCurrency.Trim().ToUpperInvariant();
        if (c == b) return 1m;
        if (rates.TryGetValue(c, out var entry) && entry.Rate > 0)
        {
            // Operation: 1 = Multiply ، 2 = Divide
            return entry.Operation == 2 ? 1m / entry.Rate : entry.Rate;
        }
        usedFallback = true;
        return 1m;
    }

    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery req, CancellationToken ct)
    {
        // ── حدّ التاريخ شامل لليوم بأكمله (مكوّن الوقت لا يستبعد القيود في نفس اليوم)
        var fromDate = req.FromDate.Date;
        var toDate = req.ToDate.Date.AddDays(1).AddTicks(-1);
        var currencyFilter = string.IsNullOrWhiteSpace(req.Currency)
            ? null
            : req.Currency.Trim().ToUpperInvariant();

        var statusInts = (req.IncludeDraft
            ? new[] { JournalEntryStatus.Posted, JournalEntryStatus.Draft }
            : new[] { JournalEntryStatus.Posted })
            .Select(s => (int)s).ToArray();

        // ───────────────────────────────────────────────────────────
        // 1) جلب نشرة الأسعار (للتقويم) — أحدث نشرة منشورة سارية على @to
        // ───────────────────────────────────────────────────────────
        string baseCur = "IQD";
        string? bulletinName = null;
        DateTime? bulletinEffectiveAt = null;
        var rates = new Dictionary<string, (decimal Rate, int Operation)>(StringComparer.OrdinalIgnoreCase);
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
                bulletinEffectiveAt = bulletin.EffectiveAt;
                foreach (var line in bulletin.Lines.Where(l => l.Rate > 0 && !string.IsNullOrWhiteSpace(l.Currency)))
                {
                    rates[line.Currency.Trim().ToUpperInvariant()] = (line.Rate, (int)line.Operation);
                }
            }
        }
        bool fxFallback = false;

        // ───────────────────────────────────────────────────────────
        // 2) جلب الحسابات (مع تطبيق فلاتر Level + LeavesOnly)
        // ───────────────────────────────────────────────────────────
        var accountsQuery = _db.Accounts.AsNoTracking().Where(a => a.IsActive);
        if (req.LeavesOnly) accountsQuery = accountsQuery.Where(a => a.IsLeaf);
        if (req.MaxLevel.HasValue && req.MaxLevel.Value > 0)
            accountsQuery = accountsQuery.Where(a => a.Level <= req.MaxLevel.Value);

        var accounts = await accountsQuery
            .OrderBy(a => a.Code)
            .Select(a => new
            {
                a.Id,
                a.Code,
                a.NameAr,
                a.Type,
                a.Nature,
                a.Level,
                a.IsLeaf,
                a.ParentId,
            })
            .ToListAsync(ct);

        if (accounts.Count == 0)
        {
            return new TrialBalanceDto
            {
                FromDate = fromDate,
                ToDate = req.ToDate.Date,
                Currency = currencyFilter,
                Valuated = req.Valuated,
                BaseCurrency = baseCur,
                FxBulletinName = bulletinName,
                FxBulletinEffectiveAt = bulletinEffectiveAt,
                FxUsedFallback = fxFallback,
                MaxLevel = req.MaxLevel,
                LeavesOnly = req.LeavesOnly,
            };
        }

        var accountIds = accounts.Select(a => a.Id).ToHashSet();

        // ───────────────────────────────────────────────────────────
        // 3) السحب الأوّل: الافتتاحي لكل (حساب × عملة)
        //    = Normal قبل @from   +   Opening حتى @to
        // ───────────────────────────────────────────────────────────
        var openingSql = @"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND (
        (e.EntryType = 1 AND e.EntryDate < @from)
     OR (e.EntryType = 2 AND e.EntryDate <= @to)
      )
  AND (@currency IS NULL OR UPPER(e.Currency) = @currency)
GROUP BY l.AccountId, e.Currency;";

        // ───────────────────────────────────────────────────────────
        // 4) السحب الثاني: حركة الفترة لكل (حساب × عملة) — Normal فقط
        // ───────────────────────────────────────────────────────────
        var periodSql = @"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND e.EntryType = 1
  AND e.EntryDate >= @from AND e.EntryDate <= @to
  AND (@currency IS NULL OR UPPER(e.Currency) = @currency)
GROUP BY l.AccountId, e.Currency;";

        var conn = _db.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);

        // (AccountId, Currency) → (Dr, Cr)
        var openingByAccCcy = new Dictionary<(int Acc, string Ccy), (decimal Dr, decimal Cr)>();
        var periodByAccCcy = new Dictionary<(int Acc, string Ccy), (decimal Dr, decimal Cr)>();

        async Task FillAsync(string sql, Dictionary<(int, string), (decimal, decimal)> target)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            AddParam(cmd, "@from", fromDate);
            AddParam(cmd, "@to", toDate);
            AddParam(cmd, "@currency", (object?)currencyFilter ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var accId = reader.GetInt32(0);
                var ccy = (reader.IsDBNull(1) ? "" : reader.GetString(1)).Trim().ToUpperInvariant();
                var dr = reader.GetDecimal(2);
                var cr = reader.GetDecimal(3);
                target[(accId, ccy)] = (dr, cr);
            }
        }

        await FillAsync(openingSql, openingByAccCcy);
        await FillAsync(periodSql, periodByAccCcy);

        // ───────────────────────────────────────────────────────────
        // 5) لو LeavesOnly=false: نُضيف لكل حساب أب تجميع أرصدة أبنائه
        //    (نسير صعوداً من كل ورقة إلى الجذر)
        // ───────────────────────────────────────────────────────────
        // نحتاج لخريطة كل حسابات النظام (ليست المُفلترة فقط) — لمعرفة سلاسل الآباء
        Dictionary<int, int?> allParents;
        if (!req.LeavesOnly)
        {
            allParents = await _db.Accounts.AsNoTracking()
                .Select(a => new { a.Id, a.ParentId })
                .ToDictionaryAsync(a => a.Id, a => a.ParentId, ct);
        }
        else
        {
            allParents = new Dictionary<int, int?>();
        }

        // قائمة جميع المفاتيح التي نحتاج تجميعها على الآباء — من بيانات الأوراق فقط
        if (!req.LeavesOnly)
        {
            void Bubble(Dictionary<(int Acc, string Ccy), (decimal Dr, decimal Cr)> src)
            {
                var leafKeys = src.Where(kv =>
                {
                    // فقط الأوراق نَبخّر منها لأنّ الأرصدة قد تأتي على أوراق فقط
                    return true; // كل القيود تُسجَّل على أوراق دائماً في نظامنا
                }).ToList();
                foreach (var kv in leafKeys)
                {
                    var (acc, ccy) = kv.Key;
                    if (!allParents.TryGetValue(acc, out var parentId)) continue;
                    while (parentId.HasValue)
                    {
                        var pkey = (parentId.Value, ccy);
                        var prev = src.TryGetValue(pkey, out var pv) ? pv : (0m, 0m);
                        src[pkey] = (prev.Item1 + kv.Value.Dr, prev.Item2 + kv.Value.Cr);
                        if (!allParents.TryGetValue(parentId.Value, out var nextParent)) break;
                        parentId = nextParent;
                    }
                }
            }
            Bubble(openingByAccCcy);
            Bubble(periodByAccCcy);
        }

        // ───────────────────────────────────────────────────────────
        // 6) بناء الصفوف النهائية + التقويم
        // ───────────────────────────────────────────────────────────
        var rows = new List<TrialBalanceRowDto>(accounts.Count);
        decimal totOpDr = 0, totOpCr = 0, totPDr = 0, totPCr = 0, totCDr = 0, totCCr = 0;
        decimal totRevenue = 0, totExpense = 0;

        foreach (var a in accounts)
        {
            // تجميع كل العملات للحساب
            decimal openDr = 0, openCr = 0, perDr = 0, perCr = 0;

            foreach (var kv in openingByAccCcy.Where(k => k.Key.Acc == a.Id))
            {
                var mult = req.Valuated
                    ? GetMultiplier(kv.Key.Ccy, baseCur, rates, ref fxFallback)
                    : 1m;
                openDr += kv.Value.Dr * mult;
                openCr += kv.Value.Cr * mult;
            }
            foreach (var kv in periodByAccCcy.Where(k => k.Key.Acc == a.Id))
            {
                var mult = req.Valuated
                    ? GetMultiplier(kv.Key.Ccy, baseCur, rates, ref fxFallback)
                    : 1m;
                perDr += kv.Value.Dr * mult;
                perCr += kv.Value.Cr * mult;
            }

            // الافتتاحي يُقدَّم كصافٍ على جانب الطبيعة، ثمّ يُحوَّل إلى عمودَي مدين/دائن
            decimal openingNet = openDr - openCr;
            decimal oDr = openingNet > 0 ? openingNet : 0m;
            decimal oCr = openingNet < 0 ? -openingNet : 0m;

            // الرصيد الختامي = الافتتاحي + حركة الفترة (مدين − دائن)
            decimal closingNet = openingNet + perDr - perCr;
            decimal cDr = closingNet > 0 ? closingNet : 0m;
            decimal cCr = closingNet < 0 ? -closingNet : 0m;

            // ‎تصفية: لا نعرض الحساب إذا لم يكن له أيّ حركة أو رصيد (يقلّل ضجيج العرض)
            if (oDr == 0 && oCr == 0 && perDr == 0 && perCr == 0 && cDr == 0 && cCr == 0)
                continue;

            rows.Add(new TrialBalanceRowDto
            {
                AccountId = a.Id,
                AccountCode = a.Code,
                AccountName = a.NameAr,
                AccountType = a.Type.ToString(),
                AccountNature = a.Nature.ToString(),
                Level = a.Level,
                IsLeaf = a.IsLeaf,
                ParentId = a.ParentId,
                OpeningDebit = oDr,
                OpeningCredit = oCr,
                PeriodDebit = perDr,
                PeriodCredit = perCr,
                ClosingDebit = cDr,
                ClosingCredit = cCr,
            });

            // الإجماليات تُجمع فقط من حسابات الأوراق (تجنّباً للتكرار عند عرض الآباء)
            if (a.IsLeaf)
            {
                totOpDr += oDr;
                totOpCr += oCr;
                totPDr += perDr;
                totPCr += perCr;
                totCDr += cDr;
                totCCr += cCr;

                // ‎حساب نتيجة الفترة: الإيرادات (طبيعة دائنة) − المصاريف (طبيعة مدينة)
                if (a.Type == AccountType.Revenue)
                {
                    totRevenue += (perCr - perDr); // صافي الإيراد
                }
                else if (a.Type == AccountType.Expense)
                {
                    totExpense += (perDr - perCr); // صافي المصروف
                }
            }
        }

        return new TrialBalanceDto
        {
            FromDate = fromDate,
            ToDate = req.ToDate.Date,
            Currency = currencyFilter,
            Valuated = req.Valuated,
            BaseCurrency = baseCur,
            FxBulletinName = bulletinName,
            FxBulletinEffectiveAt = bulletinEffectiveAt,
            FxUsedFallback = fxFallback,
            MaxLevel = req.MaxLevel,
            LeavesOnly = req.LeavesOnly,
            Rows = rows,
            TotalOpeningDebit = totOpDr,
            TotalOpeningCredit = totOpCr,
            TotalPeriodDebit = totPDr,
            TotalPeriodCredit = totPCr,
            TotalClosingDebit = totCDr,
            TotalClosingCredit = totCCr,
            TotalRevenue = totRevenue,
            TotalExpense = totExpense,
            NetIncome = totRevenue - totExpense,
        };
    }
}
