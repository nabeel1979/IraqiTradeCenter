using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

/// <summary>
/// حذف ناعم (Soft delete) — ينقل الحساب إلى سلة المهملات بدل المسح من DB.
/// شروط الحذف لا تتغيّر، لكن النتيجة:
///   • <c>IsDeleted = true</c> + <c>DeletedAt = UtcNow</c>
///   • يختفي من شجرة الحسابات وكل شاشات الاختيار (بفضل HasQueryFilter)
///   • يبقى متاحاً للاستعادة عبر <c>RestoreAccountCommand</c> أو الحذف النهائي
///     عبر <c>PermanentlyDeleteAccountCommand</c> من سلة المهملات.
/// </summary>
public class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public DeleteAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result> Handle(DeleteAccountCommand req, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.Id, ct);
        if (account is null) return Result.Failure("الحساب غير موجود");

        // 1) لا يمكن حذف حساب له أبناء (نشطون — المحذوفون في السلة لا يُحتسبون)
        var hasChildren = await _db.Accounts.AnyAsync(a => a.ParentId == req.Id, ct);
        if (hasChildren)
            return Result.Failure("لا يمكن حذف حساب لديه حسابات فرعية. احذف الفروع أولاً.");

        // 2) لا يمكن حذف حساب مستخدم في قيود محاسبية
        var hasJournalLines = await _db.JournalEntryLines.AnyAsync(l => l.AccountId == req.Id, ct);
        if (hasJournalLines)
            return Result.Failure("لا يمكن حذف الحساب — مستخدم في قيود محاسبية. يمكنك تعطيله بدلاً من ذلك.");

        // 3) لا يمكن حذف حساب مرتبط بصندوق
        var linkedToCashBox = await _db.CashBoxes.AnyAsync(b => b.AccountId == req.Id, ct);
        if (linkedToCashBox)
            return Result.Failure("لا يمكن حذف الحساب — مرتبط بصندوق. احذف الصندوق أو غيِّر حسابه أولاً.");

        // 4) لا يمكن حذف حساب مستخدم كحساب افتراضي لنوع سند
        var linkedToVoucherType = await _db.JournalVoucherTypes.AnyAsync(
            v => v.DefaultDebitAccountId == req.Id || v.DefaultCreditAccountId == req.Id, ct);
        if (linkedToVoucherType)
            return Result.Failure("لا يمكن حذف الحساب — مستخدم كحساب افتراضي في نوع سند. غيِّر الإعداد أولاً.");

        // 5) لا يمكن حذف حساب فيه رصيد افتتاحي
        if (account.OpeningBalance != 0)
            return Result.Failure("لا يمكن حذف حساب له رصيد افتتاحي. صفّر الرصيد أولاً.");

        // ‎نقل الحساب إلى السلة (Soft delete). لا نمسّ علاقات أو أعمدة أخرى — قيد
        // ‎الفهرس الفريد على Code يبقى فعّالاً ليمنع إعادة استخدامه قبل الاستعادة.
        var parentId = account.ParentId;
        account.MarkAsDeleted();
        await _db.SaveChangesAsync(ct);

        // ‎بعد إخفاء هذا الفرع، إذا أصبح الأب بلا أبناء نشطين فلنُعِد تعليمه كورقة.
        // ‎الاستعلام يستثني المحذوفين تلقائياً بفضل HasQueryFilter.
        if (parentId.HasValue)
        {
            var siblings = await _db.Accounts.AnyAsync(a => a.ParentId == parentId.Value, ct);
            if (!siblings)
            {
                var parent = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == parentId.Value, ct);
                parent?.MarkAsLeaf(true);
                await _db.SaveChangesAsync(ct);
            }
        }
        return Result.Success();
    }
}
