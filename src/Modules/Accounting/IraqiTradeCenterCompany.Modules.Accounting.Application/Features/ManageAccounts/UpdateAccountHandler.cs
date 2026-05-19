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

            account.UpdateBasic(req.NameAr, req.NameEn, req.Description);
            account.ChangeType(req.Type, req.Nature);
            if (req.IsActive) account.Activate(); else account.Deactivate();

            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Message); }
    }
}
