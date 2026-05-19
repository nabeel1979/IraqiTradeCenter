using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Settings;

[ApiController]
[Authorize]
[Route("api/currencies")]
public class CurrenciesController : ControllerBase
{
    private readonly AuthDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CurrenciesController(AuthDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>قائمة كل العملات المتاحة في النظام (مع علامتي IsEnabled/IsBase)</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool? enabledOnly = null)
    {
        IQueryable<Currency> q = _db.Currencies.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code);
        if (enabledOnly == true) q = q.Where(x => x.IsEnabled);
        var list = await q.Select(x => MapToDto(x)).ToListAsync();
        return Ok(new { success = true, data = list });
    }

    /// <summary>العملة الرئيسية الحالية</summary>
    [HttpGet("base")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBase()
    {
        var b = await _db.Currencies.AsNoTracking().FirstOrDefaultAsync(x => x.IsBase);
        if (b == null) return Ok(new { success = true, data = (CurrencyDto?)null });
        return Ok(new { success = true, data = MapToDto(b) });
    }

    private static CurrencyDto MapToDto(Currency x) => new(
        x.Code, x.NumericCode, x.NameAr, x.NameEn, x.Symbol,
        x.DecimalPlaces, x.IsEnabled, x.IsBase, x.DisplayOrder);

    /// <summary>تفعيل/تعطيل عملة. لا يمكن تعطيل العملة الرئيسية أو عملة مستخدمة في قيود.</summary>
    [HttpPut("{code}/toggle")]
    public async Task<IActionResult> Toggle(string code, [FromBody] ToggleCurrencyRequest req)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        var c = await _db.Currencies.FirstOrDefaultAsync(x => x.Code == code);
        if (c == null) return NotFound(new { success = false, message = $"العملة {code} غير موجودة" });

        if (c.IsBase && !req.IsEnabled)
            return BadRequest(new { success = false, message = "لا يمكن تعطيل العملة الرئيسية" });

        // التعطيل لا يُسمح به إذا كانت العملة مستخدمة فعلياً في قيود
        if (!req.IsEnabled && c.IsEnabled)
        {
            var used = await IsCurrencyUsedInJournalAsync(code);
            if (used)
                return BadRequest(new
                {
                    success = false,
                    message = $"لا يمكن تعطيل {code} لوجود قيود محاسبية تستخدمها — قم بحذف/تفريغ تلك القيود أولاً"
                });
        }

        c.IsEnabled = req.IsEnabled;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = _currentUser.UserId?.ToString() ?? "system";
        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>تغيير العملة الرئيسية. لا يُسمح إذا كانت العملة الحالية مستخدمة في قيود.</summary>
    [HttpPut("base")]
    public async Task<IActionResult> SetBase([FromBody] SetBaseCurrencyRequest req)
    {
        var newCode = (req.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(newCode))
            return BadRequest(new { success = false, message = "كود العملة مطلوب" });

        var newBase = await _db.Currencies.FirstOrDefaultAsync(x => x.Code == newCode);
        if (newBase == null)
            return NotFound(new { success = false, message = $"العملة {newCode} غير موجودة" });

        var currentBase = await _db.Currencies.FirstOrDefaultAsync(x => x.IsBase);
        if (currentBase != null && currentBase.Code == newCode)
            return Ok(new { success = true, message = "هذه العملة هي الأساسية أصلاً" });

        // إذا توجد عملة أساسية حالياً ومستخدمة في قيود → لا يمكن تغييرها
        if (currentBase != null)
        {
            var used = await IsCurrencyUsedInJournalAsync(currentBase.Code);
            if (used)
                return BadRequest(new
                {
                    success = false,
                    message = $"لا يمكن تغيير العملة الرئيسية ({currentBase.Code}) لوجود قيود محاسبية مرتبطة بها — احذف/فرّغ القيود أولاً"
                });
        }

        // فعّل العملة الجديدة تلقائياً وأزل علامة الأساسية من السابقة
        using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (currentBase != null && currentBase.Code != newCode)
            {
                currentBase.IsBase = false;
                currentBase.UpdatedAt = DateTime.UtcNow;
                currentBase.UpdatedBy = _currentUser.UserId?.ToString() ?? "system";
            }
            newBase.IsBase = true;
            newBase.IsEnabled = true;
            newBase.UpdatedAt = DateTime.UtcNow;
            newBase.UpdatedBy = _currentUser.UserId?.ToString() ?? "system";

            // مزامنة CompanySettings.Currency للحفاظ على التوافق الخلفي
            var settings = await _db.CompanySettings.FirstOrDefaultAsync(x => x.Id == 1);
            if (settings != null)
            {
                settings.Currency = newCode;
                settings.UpdatedAt = DateTime.UtcNow;
                settings.UpdatedBy = _currentUser.UserId?.ToString() ?? "system";
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return Ok(new { success = true, data = new { code = newCode } });
    }

    /// <summary>تحريك العملة لأعلى/أسفل في تسلسل العرض (يُبدِّل DisplayOrder مع العملة المجاورة)</summary>
    [HttpPut("{code}/move")]
    public async Task<IActionResult> Move(string code, [FromBody] MoveCurrencyRequest req)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        var dir = (req.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (dir is not ("up" or "down"))
            return BadRequest(new { success = false, message = "الاتجاه غير صالح (up/down)" });

        var current = await _db.Currencies.FirstOrDefaultAsync(x => x.Code == code);
        if (current == null) return NotFound(new { success = false, message = $"العملة {code} غير موجودة" });

        // ابحث عن العملة المجاورة بترتيب أصغر (up) أو أكبر (down) وأقرب
        Currency? neighbor = dir == "up"
            ? await _db.Currencies
                .Where(x => x.DisplayOrder < current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) < 0))
                .OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.Code)
                .FirstOrDefaultAsync()
            : await _db.Currencies
                .Where(x => x.DisplayOrder > current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) > 0))
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
                .FirstOrDefaultAsync();

        if (neighbor == null)
            return Ok(new { success = true, message = "العملة في حدود التسلسل بالفعل" });

        // تبديل قيمتي DisplayOrder
        var tmp = current.DisplayOrder;
        current.DisplayOrder = neighbor.DisplayOrder;
        neighbor.DisplayOrder = tmp;

        // إذا كانتا متساويتين بعد التبديل، أزِح أحدهما بـ ±1 لضمان الاختلاف
        if (current.DisplayOrder == neighbor.DisplayOrder)
        {
            if (dir == "up") current.DisplayOrder--;
            else current.DisplayOrder++;
        }

        var now = DateTime.UtcNow;
        var by = _currentUser.UserId?.ToString() ?? "system";
        current.UpdatedAt = now; current.UpdatedBy = by;
        neighbor.UpdatedAt = now; neighbor.UpdatedBy = by;

        await _db.SaveChangesAsync();
        return Ok(new { success = true });
    }

    /// <summary>إنشاء/تعديل عملة (Upsert)</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Upsert(string code, [FromBody] UpsertCurrencyRequest req)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code) || code.Length > 10)
            return BadRequest(new { success = false, message = "كود العملة غير صالح (1–10 أحرف)" });
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return BadRequest(new { success = false, message = "اسم العملة (عربي) مطلوب" });

        // التحقق من صحة الرقم العالمي (3 أرقام)
        var numeric = req.NumericCode?.Trim();
        if (!string.IsNullOrEmpty(numeric))
        {
            if (numeric.Length is < 1 or > 3 || !numeric.All(char.IsDigit))
                return BadRequest(new { success = false, message = "الرقم العالمي يجب أن يكون من 1 إلى 3 أرقام" });
            numeric = numeric.PadLeft(3, '0');
        }

        var c = await _db.Currencies.FirstOrDefaultAsync(x => x.Code == code);
        var isNew = c == null;
        if (isNew)
        {
            c = new Currency { Code = code, CreatedAt = DateTime.UtcNow };
            _db.Currencies.Add(c);
        }
        c!.NumericCode = string.IsNullOrEmpty(numeric) ? null : numeric;
        c.NameAr = req.NameAr.Trim();
        c.NameEn = req.NameEn?.Trim();
        c.Symbol = req.Symbol?.Trim();
        c.DecimalPlaces = Math.Clamp(req.DecimalPlaces, 0, 6);
        c.DisplayOrder = req.DisplayOrder;
        if (isNew) c.IsEnabled = req.IsEnabled;
        c.UpdatedAt = DateTime.UtcNow;
        c.UpdatedBy = _currentUser.UserId?.ToString() ?? "system";

        await _db.SaveChangesAsync();
        return Ok(new { success = true, data = MapToDto(c) });
    }

    /// <summary>هل تستخدم العملة في أي قيد محاسبي غير محذوف؟</summary>
    private async Task<bool> IsCurrencyUsedInJournalAsync(string code)
    {
        // نستخدم استعلام SQL مباشر لتجنب تبعية AccountingDbContext هنا
        // ملاحظة: JournalEntry يستخدم Soft-Delete (IsDeleted) — لذا نتجاهل القيود المحذوفة
        var conn = _db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT TOP 1 1 FROM [acc].[JournalEntries]
                            WHERE [Currency] = @c AND ISNULL([IsDeleted], 0) = 0";
        var p = cmd.CreateParameter();
        p.ParameterName = "@c";
        p.Value = code;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result != null;
    }
}

public record CurrencyDto(
    string Code,
    string? NumericCode,
    string NameAr,
    string? NameEn,
    string? Symbol,
    int DecimalPlaces,
    bool IsEnabled,
    bool IsBase,
    int DisplayOrder
);

public record ToggleCurrencyRequest(bool IsEnabled);
public record SetBaseCurrencyRequest(string Code);
public record MoveCurrencyRequest(string Direction);
public record UpsertCurrencyRequest(
    string? NumericCode,
    string NameAr,
    string? NameEn,
    string? Symbol,
    int DecimalPlaces,
    bool IsEnabled,
    int DisplayOrder
);
