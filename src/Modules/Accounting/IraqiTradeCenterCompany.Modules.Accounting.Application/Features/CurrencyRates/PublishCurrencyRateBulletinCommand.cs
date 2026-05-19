using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.CurrencyRates;

public record PublishCurrencyRateBulletinCommand(int Id) : IRequest<Result>;

public class PublishCurrencyRateBulletinHandler : IRequestHandler<PublishCurrencyRateBulletinCommand, Result>
{
    private readonly IAccountingDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PublishCurrencyRateBulletinHandler(IAccountingDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<Result> Handle(PublishCurrencyRateBulletinCommand req, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (b == null) return Result.Failure("النشرة غير موجودة");
        try
        {
            b.Publish(_currentUser.UserId?.ToString() ?? "system");
            await _db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex) { return Result.Failure(ex.Message); }
    }
}

public record ArchiveCurrencyRateBulletinCommand(int Id) : IRequest<Result>;

public class ArchiveCurrencyRateBulletinHandler : IRequestHandler<ArchiveCurrencyRateBulletinCommand, Result>
{
    private readonly IAccountingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public ArchiveCurrencyRateBulletinHandler(IAccountingDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<Result> Handle(ArchiveCurrencyRateBulletinCommand req, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (b == null) return Result.Failure("النشرة غير موجودة");
        b.Archive(_currentUser.UserId?.ToString() ?? "system");
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record DeleteCurrencyRateBulletinCommand(int Id) : IRequest<Result>;

public class DeleteCurrencyRateBulletinHandler : IRequestHandler<DeleteCurrencyRateBulletinCommand, Result>
{
    private readonly IAccountingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    public DeleteCurrencyRateBulletinHandler(IAccountingDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<Result> Handle(DeleteCurrencyRateBulletinCommand req, CancellationToken ct)
    {
        var b = await _db.CurrencyRateBulletins.FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (b == null) return Result.Failure("النشرة غير موجودة");
        if (b.Status == Domain.Enums.CurrencyRateBulletinStatus.Published)
            return Result.Failure("لا يمكن حذف نشرة منشورة. أرشفها بدلاً من الحذف");
        b.MarkAsDeleted(_currentUser.UserId?.ToString() ?? "system");
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
