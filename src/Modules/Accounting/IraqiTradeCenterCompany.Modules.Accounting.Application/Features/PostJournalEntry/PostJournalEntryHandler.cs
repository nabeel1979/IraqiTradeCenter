using IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Persistence;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Entities;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Enums;
using IraqiTradeCenterCompany.Modules.Accounting.Domain.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Exceptions;
using IraqiTradeCenterCompany.SharedKernel.Interfaces;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Features.PostJournalEntry;

public class PostJournalEntryHandler : IRequestHandler<PostJournalEntryCommand, Result<int>>
{
    private readonly IAccountingDbContext _db;
    private readonly IPeriodResolver _periods;
    private readonly ICurrentUserService _currentUser;

    public PostJournalEntryHandler(IAccountingDbContext db, IPeriodResolver periods, ICurrentUserService currentUser)
    {
        _db = db; _periods = periods; _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(PostJournalEntryCommand request, CancellationToken ct)
    {
        try
        {
            var (fyId, periodId) = await _periods.ResolveAsync(request.EntryDate, ct);

            var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
            var accounts = await _db.Accounts
                .Where(a => accountIds.Contains(a.Id) && a.IsActive).ToListAsync(ct);
            if (accounts.Count != accountIds.Count)
                return Result.Failure<int>("بعض الحسابات غير موجودة أو غير مفعّلة");
            var nonLeaf = accounts.FirstOrDefault(a => !a.IsLeaf);
            if (nonLeaf != null) return Result.Failure<int>($"الحساب '{nonLeaf.NameAr}' حساب رئيسي - لا يقبل قيوداً");

            var entry = JournalEntry.Create(request.EntryDate, fyId, periodId,
                JournalEntrySource.Manual, request.Description);

            foreach (var l in request.Lines)
            {
                if (l.IsDebit) entry.AddDebit(l.AccountId, l.Amount, l.Description);
                else entry.AddCredit(l.AccountId, l.Amount, l.Description);
            }

            entry.Post(_currentUser.UserId?.ToString() ?? "system");
            await _db.JournalEntries.AddAsync(entry, ct);
            await _db.SaveChangesAsync(ct);
            return Result.Success(entry.Id);
        }
        catch (UnbalancedJournalEntryException ex) { return Result.Failure<int>(ex.Message); }
        catch (ClosedPeriodException ex) { return Result.Failure<int>(ex.Message); }
        catch (DomainException ex) { return Result.Failure<int>(ex.Message); }
    }
}
