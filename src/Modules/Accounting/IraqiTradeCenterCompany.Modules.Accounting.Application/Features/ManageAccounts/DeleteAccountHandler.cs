using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class DeleteAccountHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public DeleteAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result> Handle(DeleteAccountCommand req, CancellationToken ct)
    {
        var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.Id, ct);
        if (account is null) return Result.Failure("الحساب غير موجود");

        // 1) لا يمكن حذف حساب له أبناء
        var hasChildren = await _db.Accounts.AnyAsync(a => a.ParentId == req.Id, ct);
        if (hasChildren)
            return Result.Failure("لا يمكن حذف حساب لديه حسابات فرعية. احذف الفروع أولاً.");

        // 2) لا يمكن حذف حساب مستخدم في قيود محاسبية
        var hasJournalLines = await _db.JournalEntryLines.AnyAsync(l => l.AccountId == req.Id, ct);
        if (hasJournalLines)
            return Result.Failure("لا يمكن حذف الحساب — مستخدم في قيود محاسبية. يمكنك تعطيله بدلاً من ذلك.");

        // 3) لا يمكن حذف حساب فيه رصيد افتتاحي
        if (account.OpeningBalance != 0)
            return Result.Failure("لا يمكن حذف حساب له رصيد افتتاحي. صفّر الرصيد أولاً.");

        // إذا كان للأب الحالي ابن واحد فقط (هذا)، يصبح الأب ورقة بعد الحذف
        var parentId = account.ParentId;

        _db.Accounts.Remove(account);
        await _db.SaveChangesAsync(ct);

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
