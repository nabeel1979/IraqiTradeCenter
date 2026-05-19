using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;

public class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Result<int>>
{
    private const int MaxLevel = 5;

    private readonly IAccountingDbContext _db;
    public CreateAccountHandler(IAccountingDbContext db) { _db = db; }

    public async Task<Result<int>> Handle(CreateAccountCommand req, CancellationToken ct)
    {
        try
        {
            var code = (req.Code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
                return Result.Failure<int>("رمز الحساب مطلوب");
            if (string.IsNullOrWhiteSpace(req.NameAr))
                return Result.Failure<int>("اسم الحساب مطلوب");

            if (await _db.Accounts.AnyAsync(a => a.Code == code, ct))
                return Result.Failure<int>($"رمز الحساب '{code}' مستخدم بالفعل");

            int level = 1;
            Account? parent = null;
            if (req.ParentId.HasValue)
            {
                parent = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == req.ParentId.Value, ct);
                if (parent is null)
                    return Result.Failure<int>("الحساب الأب غير موجود");
                level = parent.Level + 1;
            }

            if (level > MaxLevel)
                return Result.Failure<int>($"لا يمكن إنشاء حسابات أعمق من المستوى {MaxLevel}");

            var nature = req.Nature ?? Account.GetDefaultNature(req.Type);
            // إذا كان للحساب الأب أبناء، يجب أن يكون non-leaf — نضمن ذلك
            if (parent is not null && parent.IsLeaf)
                parent.MarkAsLeaf(false);

            var account = Account.Create(code, req.NameAr, req.Type, nature,
                req.ParentId, level, req.IsLeaf || level == MaxLevel);
            if (!string.IsNullOrWhiteSpace(req.Description) || !string.IsNullOrWhiteSpace(req.NameEn))
                account.UpdateBasic(req.NameAr, req.NameEn, req.Description);

            await _db.Accounts.AddAsync(account, ct);
            await _db.SaveChangesAsync(ct);
            return Result.Success(account.Id);
        }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
