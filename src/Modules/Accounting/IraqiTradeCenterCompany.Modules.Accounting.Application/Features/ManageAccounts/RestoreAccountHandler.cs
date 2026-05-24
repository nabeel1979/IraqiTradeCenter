using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class RestoreAccountHandler : IRequestHandler<RestoreAccountCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public RestoreAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result> Handle(RestoreAccountCommand req, CancellationToken ct)
    {
        // ‎يجب IgnoreQueryFilters لأن HasQueryFilter يخفي المحذوفين عن الاستعلامات الافتراضية.
        var account = await _db.Accounts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == req.Id, ct);
        if (account is null) return Result.Failure("الحساب غير موجود");
        if (!account.IsDeleted) return Result.Failure("الحساب ليس في سلة المهملات");

        // ‎لا يمكن استعادة حساب أبوه ما زال محذوفاً — لا موضع له في الشجرة.
        if (account.ParentId.HasValue)
        {
            var parent = await _db.Accounts.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == account.ParentId.Value, ct);
            if (parent != null && parent.IsDeleted)
                return Result.Failure(
                    $"لا يمكن استعادة الحساب — الأب \"{parent.NameAr}\" ({parent.Code}) ما زال في السلة. " +
                    $"استعد الأب أولاً.");
        }

        account.Restore();

        // ‎الأب أصبح غير ورقة لأنه استعاد ابناً نشطاً.
        if (account.ParentId.HasValue)
        {
            var parent = await _db.Accounts
                .FirstOrDefaultAsync(a => a.Id == account.ParentId.Value, ct);
            if (parent != null && parent.IsLeaf)
                parent.MarkAsLeaf(false);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
