using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CashBoxes;

// ─────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────

public record CashBoxCurrencyDto(
    int Id,
    string Currency,
    decimal? DebitLimit,
    decimal? CreditLimit,
    bool IsActive
);

public record CashBoxDto(
    int Id,
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    int AccountId,
    string? AccountCode,
    string? AccountName,
    bool IsActive,
    int DisplayOrder,
    List<CashBoxCurrencyDto> Currencies,
    bool HasMovements
);

public record UpsertCashBoxCurrencyDto(
    string Currency,
    decimal? DebitLimit,
    decimal? CreditLimit,
    bool IsActive
);

public record UpsertCashBoxDto(
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    int AccountId,
    bool IsActive,
    int DisplayOrder,
    List<UpsertCashBoxCurrencyDto> Currencies
);

// ─────────────────────────────────────────────────────────────────────
// Queries
// ─────────────────────────────────────────────────────────────────────

public record GetCashBoxesQuery(bool? ActiveOnly = null) : IRequest<List<CashBoxDto>>;

public class GetCashBoxesHandler : IRequestHandler<GetCashBoxesQuery, List<CashBoxDto>>
{
    private readonly IAccountingDbContext _db;
    public GetCashBoxesHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<CashBoxDto>> Handle(GetCashBoxesQuery req, CancellationToken ct)
    {
        var q = _db.CashBoxes.AsNoTracking()
            .Include(x => x.Account)
            .Include(x => x.Currencies)
            .AsQueryable();

        if (req.ActiveOnly == true) q = q.Where(x => x.IsActive);

        var rows = await q
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .ToListAsync(ct);

        // ‎الحسابات التي لها حركات (سطور قيود) — نحسبها مرة واحدة لكل الصناديق
        var accountIds = rows.Select(r => r.AccountId).Distinct().ToList();
        var accountsWithMovements = accountIds.Count == 0
            ? new HashSet<int>()
            : (await _db.JournalEntryLines.AsNoTracking()
                .Where(l => accountIds.Contains(l.AccountId))
                .Select(l => l.AccountId)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

        return rows.Select(x => MapToDto(x, accountsWithMovements.Contains(x.AccountId))).ToList();
    }

    public static CashBoxDto MapToDto(CashBox x, bool hasMovements = false) => new(
        x.Id,
        x.Code,
        x.NameAr,
        x.NameEn,
        x.Description,
        x.AccountId,
        x.Account?.Code,
        x.Account?.NameAr,
        x.IsActive,
        x.DisplayOrder,
        x.Currencies
            .OrderBy(c => c.Currency)
            .Select(c => new CashBoxCurrencyDto(c.Id, c.Currency, c.DebitLimit, c.CreditLimit, c.IsActive))
            .ToList(),
        hasMovements
    );
}

public record GetCashBoxByIdQuery(int Id) : IRequest<CashBoxDto?>;

public class GetCashBoxByIdHandler : IRequestHandler<GetCashBoxByIdQuery, CashBoxDto?>
{
    private readonly IAccountingDbContext _db;
    public GetCashBoxByIdHandler(IAccountingDbContext db) => _db = db;

    public async Task<CashBoxDto?> Handle(GetCashBoxByIdQuery req, CancellationToken ct)
    {
        var x = await _db.CashBoxes.AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Currencies)
            .FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        if (x == null) return null;

        var hasMovements = await _db.JournalEntryLines.AsNoTracking()
            .AnyAsync(l => l.AccountId == x.AccountId, ct);

        return GetCashBoxesHandler.MapToDto(x, hasMovements);
    }
}

// ─────────────────────────────────────────────────────────────────────
// Commands
// ─────────────────────────────────────────────────────────────────────

public record CreateCashBoxCommand(UpsertCashBoxDto Data) : IRequest<int>;

public class CreateCashBoxHandler : IRequestHandler<CreateCashBoxCommand, int>
{
    private readonly IAccountingDbContext _db;
    public CreateCashBoxHandler(IAccountingDbContext db) => _db = db;

    public async Task<int> Handle(CreateCashBoxCommand req, CancellationToken ct)
    {
        var d = req.Data;
        var code = (d.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code)) throw new DomainException("كود الصندوق مطلوب");

        var exists = await _db.CashBoxes.IgnoreQueryFilters()
            .AnyAsync(x => x.Code == code && !x.IsDeleted, ct);
        if (exists) throw new DomainException($"الكود {code} مستخدم بالفعل");

        await ValidateAccountAsync(_db, d.AccountId, ct);
        ValidateCurrencies(d.Currencies);

        var entity = CashBox.Create(code, d.NameAr, d.AccountId, d.NameEn, d.Description, d.IsActive, d.DisplayOrder);

        if (d.Currencies != null)
        {
            foreach (var c in d.Currencies)
            {
                entity.Currencies.Add(CashBoxCurrency.Create(0, c.Currency, c.DebitLimit, c.CreditLimit, c.IsActive));
            }
        }

        _db.CashBoxes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    internal static async Task ValidateAccountAsync(IAccountingDbContext db, int accountId, CancellationToken ct)
    {
        var ok = await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == accountId && a.IsLeaf && a.IsActive, ct);
        if (!ok) throw new DomainException("حساب الصندوق غير صالح (يجب أن يكون فرعياً مفعّلاً)");
    }

    internal static void ValidateCurrencies(IEnumerable<UpsertCashBoxCurrencyDto>? items)
    {
        if (items == null) return;
        var dups = items
            .Select(c => (c.Currency ?? string.Empty).Trim().ToUpperInvariant())
            .GroupBy(c => c)
            .Where(g => !string.IsNullOrEmpty(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (dups.Any())
            throw new DomainException($"عملات مكرّرة في الصندوق: {string.Join(", ", dups)}");
    }
}

public record UpdateCashBoxCommand(int Id, UpsertCashBoxDto Data) : IRequest<Unit>;

public class UpdateCashBoxHandler : IRequestHandler<UpdateCashBoxCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public UpdateCashBoxHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(UpdateCashBoxCommand req, CancellationToken ct)
    {
        var entity = await _db.CashBoxes
            .Include(x => x.Currencies)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("الصندوق غير موجود");

        var d = req.Data;
        await CreateCashBoxHandler.ValidateAccountAsync(_db, d.AccountId, ct);
        CreateCashBoxHandler.ValidateCurrencies(d.Currencies);

        entity.Update(d.NameAr, d.AccountId, d.NameEn, d.Description, d.IsActive, d.DisplayOrder);

        // مزامنة العملات: حذف ما يلزم، تحديث الموجود، إضافة الجديد
        var newSet = (d.Currencies ?? new List<UpsertCashBoxCurrencyDto>())
            .Select(c => new { Currency = (c.Currency ?? string.Empty).Trim().ToUpperInvariant(), c.DebitLimit, c.CreditLimit, c.IsActive })
            .Where(c => !string.IsNullOrEmpty(c.Currency))
            .ToList();
        var newCodes = newSet.Select(c => c.Currency).ToHashSet();

        foreach (var existing in entity.Currencies.ToList())
        {
            if (!newCodes.Contains(existing.Currency))
            {
                existing.MarkAsDeleted();
                _db.CashBoxCurrencies.Remove(existing);
            }
        }

        foreach (var c in newSet)
        {
            var existing = entity.Currencies.FirstOrDefault(e => e.Currency == c.Currency && !e.IsDeleted);
            if (existing != null)
            {
                existing.Update(c.DebitLimit, c.CreditLimit, c.IsActive);
            }
            else
            {
                entity.Currencies.Add(CashBoxCurrency.Create(entity.Id, c.Currency, c.DebitLimit, c.CreditLimit, c.IsActive));
            }
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record ToggleCashBoxCommand(int Id, bool IsActive) : IRequest<Unit>;

public class ToggleCashBoxHandler : IRequestHandler<ToggleCashBoxCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public ToggleCashBoxHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(ToggleCashBoxCommand req, CancellationToken ct)
    {
        var entity = await _db.CashBoxes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("الصندوق غير موجود");
        entity.SetActive(req.IsActive);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record DeleteCashBoxCommand(int Id) : IRequest<Unit>;

public class DeleteCashBoxHandler : IRequestHandler<DeleteCashBoxCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public DeleteCashBoxHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteCashBoxCommand req, CancellationToken ct)
    {
        var entity = await _db.CashBoxes
            .Include(x => x.Currencies)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("الصندوق غير موجود");

        // ‎الحماية: لا يُسمح بحذف صندوق له حركات (سطور قيود) على حسابه المرتبط.
        // ‎الحذف هنا soft-delete، لكن الإبقاء يحفظ سلامة المراجع التاريخية.
        var hasMovements = await _db.JournalEntryLines.AsNoTracking()
            .AnyAsync(l => l.AccountId == entity.AccountId, ct);
        if (hasMovements)
            throw new DomainException(
                $"لا يمكن حذف الصندوق \"{entity.NameAr}\" لأن الحساب المرتبط به ({entity.AccountId}) عليه حركات محاسبية. " +
                "يمكنك تعطيله بدلاً من حذفه."
            );

        entity.MarkAsDeleted();
        foreach (var c in entity.Currencies) c.MarkAsDeleted();
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record MoveCashBoxCommand(int Id, string Direction) : IRequest<Unit>;

public class MoveCashBoxHandler : IRequestHandler<MoveCashBoxCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public MoveCashBoxHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(MoveCashBoxCommand req, CancellationToken ct)
    {
        var dir = (req.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (dir is not ("up" or "down")) throw new DomainException("الاتجاه غير صالح");

        var current = await _db.CashBoxes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("الصندوق غير موجود");

        CashBox? neighbor = dir == "up"
            ? await _db.CashBoxes
                .Where(x => x.DisplayOrder < current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) < 0))
                .OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.Code)
                .FirstOrDefaultAsync(ct)
            : await _db.CashBoxes
                .Where(x => x.DisplayOrder > current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) > 0))
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
                .FirstOrDefaultAsync(ct);

        if (neighbor == null) return Unit.Value;

        var tmp = current.DisplayOrder;
        current.SetDisplayOrder(neighbor.DisplayOrder);
        neighbor.SetDisplayOrder(tmp);
        if (current.DisplayOrder == neighbor.DisplayOrder)
        {
            if (dir == "up") current.SetDisplayOrder(current.DisplayOrder - 1);
            else current.SetDisplayOrder(current.DisplayOrder + 1);
        }
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
