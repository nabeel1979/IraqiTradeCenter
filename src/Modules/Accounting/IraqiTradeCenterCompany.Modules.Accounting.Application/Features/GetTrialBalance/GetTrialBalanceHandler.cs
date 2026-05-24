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
        // 0) السنة المالية المعنيّة بالتقرير:
        //    نعتمد قاعدة محاسبية أساسية: حسابات الأرباح/الخسائر (Revenue/Expense)
        //    تُصفَّر بنهاية كل سنة مالية عبر قيود الإقفال — فلا يُرَحَّل أيّ
        //    رصيد منها للسنة التالية. لذا عند احتساب "الافتتاحي" لتلك الحسابات
        //    نقيّد المدى بـ [بداية السنة المالية للـ @from … @from)، بدلاً من
        //    "كامل التاريخ قبل @from" المُستخدَم لحسابات الميزانية.
        //
        //    الأولوية في تحديد السنة المالية:
        //      1) السنة المالية التي تحتوي @from   — يدعم التقارير التاريخية.
        //      2) السنة المُفَعَّلة (IsActive=true)  — احتياطي حين لا تحتوي أيّ سنة @from.
        //
        //    إن لم نتمكّن من تحديد سنة مالية، نُبقي السلوك السابق (يَجمع كل
        //    التاريخ) لتفادي تغيير غير متوقَّع لمستخدمين بلا سنوات مالية.
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
        // 2) جلب جميع الحسابات النشطة دفعةً واحدة، ثم نشتق:
        //    • allAccounts  : كل الحسابات (للوصول إلى ParentId / Type / IsLeaf
        //                      عند تجميع الأرصدة على الآباء وحساب الإجماليات).
        //    • leafAccounts : الأوراق فقط — ستُستخدم لحساب الإجماليات و
        //                      نتيجة الفترة (الإيرادات/المصاريف). الإجماليات
        //                      تُجمَع دائماً من الأوراق بصرف النظر عمّا يَعرضه
        //                      المستخدم (المستوى/الأبناء فقط) لأنّ هذه الفلاتر
        //                      تخصّ العرض فقط ولا يجوز أن تُصفّر الإجمالي.
        //    • accounts     : الحسابات التي ستُعرَض في الجدول (وفق فلاتر العرض).
        // ───────────────────────────────────────────────────────────
        var allAccounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.IsActive)
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

        var leafAccounts = allAccounts.Where(a => a.IsLeaf).ToList();
        var accounts = allAccounts
            .Where(a => (!req.LeavesOnly || a.IsLeaf)
                     && (!req.MaxLevel.HasValue || req.MaxLevel.Value <= 0 || a.Level <= req.MaxLevel.Value))
            .ToList();

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

        // ───────────────────────────────────────────────────────────
        // 3) السحب الأوّل: الافتتاحي لكل (حساب × عملة)
        //    قاعدة التضمين تختلف بحسب نوع الحساب:
        //      • أصول/خصوم/حقوق ملكية (1،2،3): جميع قيود Normal قبل @from + قيود Opening حتى @to
        //      • إيرادات/مصاريف     (4،5): فقط قيود Normal من بداية السنة المالية
        //                                     الحالية (@plFyStart) حتى @from. قيود Opening
        //                                     لا تُحسب لأن طبيعة هذه الحسابات تُصفَّر سنوياً.
        //    إن كان @plFyStart NULL (لا توجد سنة مالية)، نُبقي السلوك القديم لـ P&L
        //    (يَجمع كامل التاريخ) لتفادي حذف بيانات بدون قاعدة بديلة.
        var openingSql = @"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND (
        -- ‎الميزانية: كامل التاريخ قبل @from (لـ Normal) + Opening حتى @to
        (a.[Type] IN (1,2,3) AND (
            (e.EntryType = 1 AND e.EntryDate < @from)
         OR (e.EntryType = 2 AND e.EntryDate <= @to)
        ))
     OR
        -- ‎الأرباح/الخسائر: فقط ضمن السنة المالية الحالية
        (a.[Type] IN (4,5) AND e.EntryType = 1 AND e.EntryDate < @from
            AND (@plFyStart IS NULL OR e.EntryDate >= @plFyStart))
      )
  AND (@currency IS NULL OR UPPER(e.Currency) = @currency)
GROUP BY l.AccountId, e.Currency;";

        // ───────────────────────────────────────────────────────────
        // 4) السحب الثاني: حركة الفترة لكل (حساب × عملة) — Normal فقط
        //    لحسابات الأرباح/الخسائر نقصر الحركة كذلك على نطاق السنة المالية
        //    الحالية حتى لو امتدّ مدى التقرير لسنوات سابقة (لأن قيود تلك
        //    السنوات تخصّ نتيجة سنة منفصلة، لا تُجمع مع السنة الحالية).
        // ───────────────────────────────────────────────────────────
        var periodSql = @"
SELECT l.AccountId, e.Currency,
       ISNULL(SUM(CASE WHEN l.IsDebit = 1 THEN l.Amount ELSE 0 END), 0) AS Dr,
       ISNULL(SUM(CASE WHEN l.IsDebit = 0 THEN l.Amount ELSE 0 END), 0) AS Cr
FROM acc.JournalEntryLines l
INNER JOIN acc.JournalEntries e ON e.Id = l.JournalEntryId
INNER JOIN acc.Accounts a       ON a.Id = l.AccountId
WHERE l.IsDeleted = 0 AND e.IsDeleted = 0
  AND e.[Status] IN (" + string.Join(",", statusInts) + @")
  AND e.EntryType = 1
  AND e.EntryDate >= @from AND e.EntryDate <= @to
  AND (a.[Type] IN (1,2,3)
       OR (a.[Type] IN (4,5)
            AND (@plFyStart IS NULL OR e.EntryDate >= @plFyStart)))
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
            // ‎بداية السنة المالية المُستخدَمة لحدّ احتساب رصيد الأرباح/الخسائر.
            AddParam(cmd, "@plFyStart", (object?)plFyStart ?? DBNull.Value);
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
        //    (نسير صعوداً من كل ورقة إلى الجذر) — لازم لعرض الأرصدة على
        //    الحسابات الأمّ عند تقييد المستوى أو عند `LeavesOnly=false`.
        // ───────────────────────────────────────────────────────────
        var allParents = allAccounts.ToDictionary(a => a.Id, a => a.ParentId);

        if (!req.LeavesOnly)
        {
            void Bubble(Dictionary<(int Acc, string Ccy), (decimal Dr, decimal Cr)> src)
            {
                // ‎كل القيود تُسجَّل على الأوراق فقط، فننسخ المفاتيح أوّلاً تجنّباً
                // ‎لتعديل القاموس أثناء التكرار، ثم نصعد من كل ورقة حتى الجذر.
                var leafKeys = src.ToList();
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
        // 6) دالّة تجميع المبالغ لحسابٍ واحد عبر كل العملات (مع التقويم).
        //    تُستخدم في حلقتين منفصلتين: واحدة لبناء الصفوف المعروضة،
        //    وأخرى لحساب الإجماليات و نتيجة الفترة من جميع الأوراق دائماً.
        // ───────────────────────────────────────────────────────────
        (decimal openDr, decimal openCr, decimal perDr, decimal perCr) AggregateForAccount(int accId)
        {
            decimal oD = 0, oC = 0, pD = 0, pC = 0;
            foreach (var kv in openingByAccCcy.Where(k => k.Key.Acc == accId))
            {
                var mult = req.Valuated
                    ? GetMultiplier(kv.Key.Ccy, baseCur, rates, ref fxFallback)
                    : 1m;
                oD += kv.Value.Dr * mult;
                oC += kv.Value.Cr * mult;
            }
            foreach (var kv in periodByAccCcy.Where(k => k.Key.Acc == accId))
            {
                var mult = req.Valuated
                    ? GetMultiplier(kv.Key.Ccy, baseCur, rates, ref fxFallback)
                    : 1m;
                pD += kv.Value.Dr * mult;
                pC += kv.Value.Cr * mult;
            }
            return (oD, oC, pD, pC);
        }

        // ───────────────────────────────────────────────────────────
        // 7) حساب الإجماليات و نتيجة الفترة (الإيرادات/المصاريف) من جميع
        //    الأوراق النشطة دائماً — بصرف النظر عن فلاتر العرض (LeavesOnly /
        //    MaxLevel). فلاتر العرض تخصّ الجدول فقط ولا يجوز أن تُصفّر الإجمالي.
        // ───────────────────────────────────────────────────────────
        decimal totOpDr = 0, totOpCr = 0, totPDr = 0, totPCr = 0, totCDr = 0, totCCr = 0;
        decimal totRevenue = 0, totExpense = 0;

        foreach (var a in leafAccounts)
        {
            var (openDr, openCr, perDr, perCr) = AggregateForAccount(a.Id);
            var openingNet = openDr - openCr;
            var oDr = openingNet > 0 ? openingNet : 0m;
            var oCr = openingNet < 0 ? -openingNet : 0m;
            var closingNet = openingNet + perDr - perCr;
            var cDr = closingNet > 0 ? closingNet : 0m;
            var cCr = closingNet < 0 ? -closingNet : 0m;

            totOpDr += oDr;
            totOpCr += oCr;
            totPDr += perDr;
            totPCr += perCr;
            totCDr += cDr;
            totCCr += cCr;

            // ‎نتيجة الفترة: الإيرادات (طبيعة دائنة) − المصاريف (طبيعة مدينة)
            if (a.Type == AccountType.Revenue) totRevenue += (perCr - perDr);
            else if (a.Type == AccountType.Expense) totExpense += (perDr - perCr);
        }

        // ───────────────────────────────────────────────────────────
        // 8) بناء الصفوف المعروضة (وفق فلاتر MaxLevel/LeavesOnly).
        // ───────────────────────────────────────────────────────────
        var rows = new List<TrialBalanceRowDto>(accounts.Count);

        foreach (var a in accounts)
        {
            var (openDr, openCr, perDr, perCr) = AggregateForAccount(a.Id);
            var openingNet = openDr - openCr;
            var oDr = openingNet > 0 ? openingNet : 0m;
            var oCr = openingNet < 0 ? -openingNet : 0m;
            var closingNet = openingNet + perDr - perCr;
            var cDr = closingNet > 0 ? closingNet : 0m;
            var cCr = closingNet < 0 ? -closingNet : 0m;

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
