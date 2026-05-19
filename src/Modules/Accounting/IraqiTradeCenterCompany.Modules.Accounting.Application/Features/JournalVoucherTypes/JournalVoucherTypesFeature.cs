using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.JournalVoucherTypes;

// ─────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────

public record JournalVoucherTypeDto(
    int Id,
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    int? DefaultDebitAccountId,
    string? DefaultDebitAccountCode,
    string? DefaultDebitAccountName,
    int? DefaultCreditAccountId,
    string? DefaultCreditAccountCode,
    string? DefaultCreditAccountName,
    bool IsEnabled,
    bool IsSystem,
    int DisplayOrder,
    string Nature,
    bool ShowInSidebar
);

public record UpsertJournalVoucherTypeDto(
    string Code,
    string NameAr,
    string? NameEn,
    string? Description,
    int? DefaultDebitAccountId,
    int? DefaultCreditAccountId,
    bool IsEnabled,
    int DisplayOrder,
    string Nature = "Mixed",
    bool ShowInSidebar = false
);

// ─────────────────────────────────────────────────────────────────────
// Queries
// ─────────────────────────────────────────────────────────────────────

public record GetJournalVoucherTypesQuery(bool? EnabledOnly = null) : IRequest<List<JournalVoucherTypeDto>>;

public class GetJournalVoucherTypesHandler : IRequestHandler<GetJournalVoucherTypesQuery, List<JournalVoucherTypeDto>>
{
    private readonly IAccountingDbContext _db;
    public GetJournalVoucherTypesHandler(IAccountingDbContext db) => _db = db;

    public async Task<List<JournalVoucherTypeDto>> Handle(GetJournalVoucherTypesQuery req, CancellationToken ct)
    {
        var q = _db.JournalVoucherTypes.AsNoTracking()
            .Include(x => x.DefaultDebitAccount)
            .Include(x => x.DefaultCreditAccount)
            .AsQueryable();

        if (req.EnabledOnly == true) q = q.Where(x => x.IsEnabled);

        var rows = await q
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
            .ToListAsync(ct);

        return rows.Select(MapToDto).ToList();
    }

    public static JournalVoucherTypeDto MapToDto(JournalVoucherType x) => new(
        x.Id,
        x.Code,
        x.NameAr,
        x.NameEn,
        x.Description,
        x.DefaultDebitAccountId,
        x.DefaultDebitAccount?.Code,
        x.DefaultDebitAccount?.NameAr,
        x.DefaultCreditAccountId,
        x.DefaultCreditAccount?.Code,
        x.DefaultCreditAccount?.NameAr,
        x.IsEnabled,
        x.IsSystem,
        x.DisplayOrder,
        x.Nature.ToString(),
        x.ShowInSidebar);
}

internal static class VoucherNatureParser
{
    public static VoucherNature Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return VoucherNature.Mixed;
        return value.Trim().ToLowerInvariant() switch
        {
            "debit" or "1" => VoucherNature.Debit,
            "credit" or "2" => VoucherNature.Credit,
            _ => VoucherNature.Mixed,
        };
    }
}

public record GetJournalVoucherTypeByIdQuery(int Id) : IRequest<JournalVoucherTypeDto?>;

public class GetJournalVoucherTypeByIdHandler : IRequestHandler<GetJournalVoucherTypeByIdQuery, JournalVoucherTypeDto?>
{
    private readonly IAccountingDbContext _db;
    public GetJournalVoucherTypeByIdHandler(IAccountingDbContext db) => _db = db;

    public async Task<JournalVoucherTypeDto?> Handle(GetJournalVoucherTypeByIdQuery req, CancellationToken ct)
    {
        var x = await _db.JournalVoucherTypes.AsNoTracking()
            .Include(t => t.DefaultDebitAccount)
            .Include(t => t.DefaultCreditAccount)
            .FirstOrDefaultAsync(t => t.Id == req.Id, ct);
        return x == null ? null : GetJournalVoucherTypesHandler.MapToDto(x);
    }
}

// ─────────────────────────────────────────────────────────────────────
// Commands
// ─────────────────────────────────────────────────────────────────────

public record CreateJournalVoucherTypeCommand(UpsertJournalVoucherTypeDto Data) : IRequest<int>;

public class CreateJournalVoucherTypeHandler : IRequestHandler<CreateJournalVoucherTypeCommand, int>
{
    private readonly IAccountingDbContext _db;
    public CreateJournalVoucherTypeHandler(IAccountingDbContext db) => _db = db;

    public async Task<int> Handle(CreateJournalVoucherTypeCommand req, CancellationToken ct)
    {
        var d = req.Data;
        var code = (d.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
            throw new DomainException("كود نوع السند مطلوب");

        var exists = await _db.JournalVoucherTypes
            .IgnoreQueryFilters()
            .AnyAsync(x => x.Code == code && !x.IsDeleted, ct);
        if (exists) throw new DomainException($"الكود {code} مستخدم بالفعل");

        await ValidateAccountsAsync(_db, d.DefaultDebitAccountId, d.DefaultCreditAccountId, ct);

        var entity = JournalVoucherType.Create(
            code,
            d.NameAr,
            d.NameEn,
            d.Description,
            d.DefaultDebitAccountId,
            d.DefaultCreditAccountId,
            d.IsEnabled,
            isSystem: false,
            d.DisplayOrder,
            VoucherNatureParser.Parse(d.Nature),
            d.ShowInSidebar);

        _db.JournalVoucherTypes.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    internal static async Task ValidateAccountsAsync(IAccountingDbContext db, int? debitId, int? creditId, CancellationToken ct)
    {
        if (debitId.HasValue)
        {
            var ok = await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == debitId.Value && a.IsLeaf, ct);
            if (!ok) throw new DomainException("حساب المدين الافتراضي غير صالح (يجب أن يكون فرعياً ضمن الدليل)");
        }
        if (creditId.HasValue)
        {
            var ok = await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == creditId.Value && a.IsLeaf, ct);
            if (!ok) throw new DomainException("حساب الدائن الافتراضي غير صالح (يجب أن يكون فرعياً ضمن الدليل)");
        }
        if (debitId.HasValue && creditId.HasValue && debitId.Value == creditId.Value)
            throw new DomainException("لا يجوز أن يكون حساب المدين والدائن متطابقين");
    }
}

public record UpdateJournalVoucherTypeCommand(int Id, UpsertJournalVoucherTypeDto Data) : IRequest<Unit>;

public class UpdateJournalVoucherTypeHandler : IRequestHandler<UpdateJournalVoucherTypeCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public UpdateJournalVoucherTypeHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(UpdateJournalVoucherTypeCommand req, CancellationToken ct)
    {
        var entity = await _db.JournalVoucherTypes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("نوع السند غير موجود");

        var d = req.Data;
        await CreateJournalVoucherTypeHandler.ValidateAccountsAsync(_db, d.DefaultDebitAccountId, d.DefaultCreditAccountId, ct);

        entity.Update(
            d.NameAr,
            d.NameEn,
            d.Description,
            d.DefaultDebitAccountId,
            d.DefaultCreditAccountId,
            d.IsEnabled,
            d.DisplayOrder,
            VoucherNatureParser.Parse(d.Nature),
            d.ShowInSidebar);

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record ToggleJournalVoucherTypeCommand(int Id, bool IsEnabled) : IRequest<Unit>;

public class ToggleJournalVoucherTypeHandler : IRequestHandler<ToggleJournalVoucherTypeCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public ToggleJournalVoucherTypeHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(ToggleJournalVoucherTypeCommand req, CancellationToken ct)
    {
        var entity = await _db.JournalVoucherTypes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("نوع السند غير موجود");
        entity.SetEnabled(req.IsEnabled);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record DeleteJournalVoucherTypeCommand(int Id) : IRequest<Unit>;

public class DeleteJournalVoucherTypeHandler : IRequestHandler<DeleteJournalVoucherTypeCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public DeleteJournalVoucherTypeHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(DeleteJournalVoucherTypeCommand req, CancellationToken ct)
    {
        var entity = await _db.JournalVoucherTypes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("نوع السند غير موجود");

        if (entity.IsSystem)
            throw new DomainException("لا يمكن حذف نوع سند مدمج بالنظام");

        // Soft delete (يحترم QueryFilter !IsDeleted)
        entity.MarkAsDeleted();
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

public record MoveJournalVoucherTypeCommand(int Id, string Direction) : IRequest<Unit>;

public class MoveJournalVoucherTypeHandler : IRequestHandler<MoveJournalVoucherTypeCommand, Unit>
{
    private readonly IAccountingDbContext _db;
    public MoveJournalVoucherTypeHandler(IAccountingDbContext db) => _db = db;

    public async Task<Unit> Handle(MoveJournalVoucherTypeCommand req, CancellationToken ct)
    {
        var dir = (req.Direction ?? string.Empty).Trim().ToLowerInvariant();
        if (dir is not ("up" or "down"))
            throw new DomainException("الاتجاه غير صالح (up/down)");

        var current = await _db.JournalVoucherTypes.FirstOrDefaultAsync(x => x.Id == req.Id, ct)
            ?? throw new DomainException("نوع السند غير موجود");

        JournalVoucherType? neighbor = dir == "up"
            ? await _db.JournalVoucherTypes
                .Where(x => x.DisplayOrder < current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) < 0))
                .OrderByDescending(x => x.DisplayOrder).ThenByDescending(x => x.Code)
                .FirstOrDefaultAsync(ct)
            : await _db.JournalVoucherTypes
                .Where(x => x.DisplayOrder > current.DisplayOrder
                            || (x.DisplayOrder == current.DisplayOrder && string.Compare(x.Code, current.Code) > 0))
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code)
                .FirstOrDefaultAsync(ct);

        if (neighbor == null) return Unit.Value; // عند الحدود

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
