using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand, Result>
{
    private readonly IAccountingDbContext _db;
    public UpdateAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result> Handle(UpdateAccountCommand req, CancellationToken ct)
    {
        try
        {
            var account = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.Id, ct);
            if (account is null) return Result.Failure("الحساب غير موجود");

            if (string.IsNullOrWhiteSpace(req.NameAr))
                return Result.Failure("اسم الحساب مطلوب");

            var nameAr = req.NameAr.Trim();

            // ‎فحص تفرّد الاسم بين الإخوة — يستثني الحساب الحالي ليُسمح بحفظ بدون
            // ‎تغيير الاسم، ويُضمّن المعطَّلين كي لا يحدث تضارب عند تفعيل أيٍّ منهم لاحقاً.
            var pid = account.ParentId;
            var dupName = await _db.Accounts.AsNoTracking()
                .AnyAsync(a => a.Id != req.Id && a.ParentId == pid && a.NameAr == nameAr, ct);
            if (dupName)
                return Result.Failure(
                    $"الاسم '{nameAr}' مستخدم بالفعل تحت نفس الأب — استخدم اسماً مختلفاً");

            account.UpdateBasic(req.NameAr, req.NameEn, req.Description);
            account.ChangeType(req.Type, req.Nature);
            if (req.IsActive) account.Activate(); else account.Deactivate();

            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Message); }
    }
}
