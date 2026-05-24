using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

/// <summary>
/// يُرجع تفصيل استخدام الحساب (من قيود/صناديق/سندات/رصيد افتتاحي/أبناء).
/// يُستخدم في الواجهة لشرح للمستخدم لماذا الحساب "مستخدم" وكيف يفك ارتباطه.
/// </summary>
public record GetAccountUsageQuery(int AccountId) : IRequest<AccountUsageDto>;

public class AccountUsageDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;

    /// <summary>عدد سطور القيود (المحاسبية) المرتبطة بهذا الحساب وغير محذوفة.</summary>
    public int JournalLineCount { get; set; }
    /// <summary>عدد القيود المسوّدة (Draft) المتأثرة.</summary>
    public int DraftEntryCount { get; set; }
    /// <summary>عدد القيود المرحّلة (Posted) المتأثرة.</summary>
    public int PostedEntryCount { get; set; }
    /// <summary>عدد القيود المعكوسة (Reversed) المتأثرة.</summary>
    public int ReversedEntryCount { get; set; }

    /// <summary>عينة من القيود الأخيرة (آخر 5) لمساعدة المستخدم في تحديدها.</summary>
    public List<RelatedJournalEntry> RecentEntries { get; set; } = new();

    public List<RelatedCashBox> CashBoxes { get; set; } = new();
    public List<RelatedVoucherType> VoucherTypesAsDebit { get; set; } = new();
    public List<RelatedVoucherType> VoucherTypesAsCredit { get; set; } = new();

    public bool HasOpeningBalance { get; set; }
    public decimal OpeningBalance { get; set; }

    public int ChildrenCount { get; set; }

    /// <summary>
    /// مستخدم بأي شكل (يمنع جعله أباً عبر إضافة فرع، ويمنع حذفه).
    /// نفس منطق IsUsed على AccountDto.
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>قائمة أسباب نصية موجزة بالعربية تُعرض للمستخدم.</summary>
    public List<string> Reasons { get; set; } = new();
}

public record RelatedJournalEntry(
    int Id,
    string EntryNumber,
    DateTime EntryDate,
    string Status,
    decimal Amount,
    bool IsDebit);

public record RelatedCashBox(int Id, string Name, string Code);

public record RelatedVoucherType(int Id, string Code, string NameAr);

public class GetAccountUsageHandler : IRequestHandler<GetAccountUsageQuery, AccountUsageDto>
{
    private readonly IAccountingDbContext _db;
    public GetAccountUsageHandler(IAccountingDbContext db) => _db = db;

    public async Task<AccountUsageDto> Handle(GetAccountUsageQuery req, CancellationToken ct)
    {
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == req.AccountId, ct);

        if (account == null)
            return new AccountUsageDto { AccountId = req.AccountId, AccountCode = "—", AccountName = "غير موجود" };

        var dto = new AccountUsageDto
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.NameAr,
            HasOpeningBalance = account.OpeningBalance != 0m,
            OpeningBalance = account.OpeningBalance,
        };

        // ── سطور القيود (يستثني المحذوفين تلقائياً عبر الـ global query filter) ─────
        var lines = await (
            from l in _db.JournalEntryLines.AsNoTracking()
            where l.AccountId == account.Id
            join e in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals e.Id
            select new { l.Id, l.IsDebit, l.Amount, e.EntryNumber, e.EntryDate, e.Status, EntryId = e.Id }
        ).ToListAsync(ct);

        dto.JournalLineCount = lines.Count;
        var byEntry = lines
            .GroupBy(x => x.EntryId)
            .Select(g => g.First())
            .ToList();
        dto.DraftEntryCount    = byEntry.Count(x => x.Status == Domain.Enums.JournalEntryStatus.Draft);
        dto.PostedEntryCount   = byEntry.Count(x => x.Status == Domain.Enums.JournalEntryStatus.Posted);
        dto.ReversedEntryCount = byEntry.Count(x => x.Status == Domain.Enums.JournalEntryStatus.Reversed);

        dto.RecentEntries = byEntry
            .OrderByDescending(x => x.EntryDate)
            .ThenByDescending(x => x.EntryId)
            .Take(5)
            .Select(x => new RelatedJournalEntry(
                x.EntryId,
                x.EntryNumber,
                x.EntryDate,
                x.Status.ToString(),
                x.Amount,
                x.IsDebit))
            .ToList();

        // ── الصناديق المرتبطة ─────────────────────────────────────────────
        dto.CashBoxes = await _db.CashBoxes.AsNoTracking()
            .Where(b => b.AccountId == account.Id)
            .Select(b => new RelatedCashBox(b.Id, b.NameAr, b.Code))
            .ToListAsync(ct);

        // ── أنواع السندات التي تستخدم الحساب كافتراضي ─────────────────────
        dto.VoucherTypesAsDebit = await _db.JournalVoucherTypes.AsNoTracking()
            .Where(v => v.DefaultDebitAccountId == account.Id)
            .Select(v => new RelatedVoucherType(v.Id, v.Code, v.NameAr))
            .ToListAsync(ct);

        dto.VoucherTypesAsCredit = await _db.JournalVoucherTypes.AsNoTracking()
            .Where(v => v.DefaultCreditAccountId == account.Id)
            .Select(v => new RelatedVoucherType(v.Id, v.Code, v.NameAr))
            .ToListAsync(ct);

        // ── الأبناء ─────────────────────────────────────────────────────────
        dto.ChildrenCount = await _db.Accounts.AsNoTracking()
            .Where(a => a.ParentId == account.Id && a.IsActive)
            .CountAsync(ct);

        // ── أسباب الاستخدام (نص بسيط للعرض) ───────────────────────────────
        if (dto.JournalLineCount > 0)
            dto.Reasons.Add(
                $"يوجد {dto.JournalLineCount} سطر قيد ({dto.PostedEntryCount} مرحَّل، {dto.DraftEntryCount} مسودة، {dto.ReversedEntryCount} معكوس)");
        if (dto.CashBoxes.Count > 0)
            dto.Reasons.Add($"مرتبط بـ {dto.CashBoxes.Count} صندوق: {string.Join("، ", dto.CashBoxes.Select(c => c.Name))}");
        if (dto.VoucherTypesAsDebit.Count > 0)
            dto.Reasons.Add($"حساب مدين افتراضي لأنواع السندات: {string.Join("، ", dto.VoucherTypesAsDebit.Select(v => v.NameAr))}");
        if (dto.VoucherTypesAsCredit.Count > 0)
            dto.Reasons.Add($"حساب دائن افتراضي لأنواع السندات: {string.Join("، ", dto.VoucherTypesAsCredit.Select(v => v.NameAr))}");
        if (dto.HasOpeningBalance)
            dto.Reasons.Add($"يحوي رصيداً افتتاحياً: {dto.OpeningBalance:N3}");
        if (dto.ChildrenCount > 0)
            dto.Reasons.Add($"يحوي {dto.ChildrenCount} حساب فرعي");

        dto.IsUsed = dto.Reasons.Count > 0;
        return dto;
    }
}
