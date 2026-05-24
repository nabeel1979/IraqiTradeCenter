using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class PermanentlyDeleteAccountHandler : IRequestHandler<PermanentlyDeleteAccountCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public PermanentlyDeleteAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result> Handle(PermanentlyDeleteAccountCommand req, CancellationToken ct)
    {
        var account = await _db.Accounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == req.Id, ct);
        if (account is null) return Result.Failure("الحساب غير موجود");
        if (!account.IsDeleted)
            return Result.Failure(
                "الحذف النهائي مسموح فقط للحسابات الموجودة في سلة المهملات. احذف الحساب أولاً.");

        // ‎فحوصات دفاعية — لا ينبغي أن تكون لحساب في السلة هذه المراجع، لكن نتأكد
        // ‎كي لا نكسر السلامة المرجعية في حالات استثنائية.
        if (await _db.Accounts.IgnoreQueryFilters().AnyAsync(a => a.ParentId == req.Id, ct))
            return Result.Failure(
                "لا يمكن الحذف النهائي — للحساب فروع (سواء نشطة أو في السلة). احذفها نهائياً أولاً.");

        if (await _db.JournalEntryLines.AnyAsync(l => l.AccountId == req.Id, ct))
            return Result.Failure("لا يمكن الحذف النهائي — للحساب قيود محاسبية مرتبطة.");

        if (await _db.CashBoxes.AnyAsync(b => b.AccountId == req.Id, ct))
            return Result.Failure("لا يمكن الحذف النهائي — الحساب مرتبط بصندوق.");

        if (await _db.JournalVoucherTypes.AnyAsync(
                v => v.DefaultDebitAccountId == req.Id || v.DefaultCreditAccountId == req.Id, ct))
            return Result.Failure("لا يمكن الحذف النهائي — الحساب مستخدم في نوع سند.");

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
