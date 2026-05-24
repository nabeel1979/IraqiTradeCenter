using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.GetAccountsTrash;
using IraqiTradeCenterCompany.Modules.Accounting.Application.Features.ManageAccounts;
using IraqiTradeCenterCompany.SharedKernel.Models;
using MediatR;

namespace IraqiTradeCenterCompany.API.Trash.Providers;

/// <summary>
/// مُزوِّد سلة الحسابات — يستخدم Mediator commands القائمة (Restore/Permanently…)
/// لإعادة استعمال نفس قواعد التحقّق المُطبَّقة من شاشة شجرة الحسابات.
/// </summary>
public class AccountTrashProvider : ITrashProvider
{
    private readonly IMediator _mediator;
    public AccountTrashProvider(IMediator mediator) { _mediator = mediator; }

    public string EntityType => "Account";

    public async Task<List<TrashItemDto>> ListAsync(CancellationToken ct)
    {
        var items = await _mediator.Send(new GetAccountsTrashQuery(), ct);
        return items.Select(a => new TrashItemDto
        {
            EntityType = EntityType,
            EntityTypeLabel = "حساب",
            Module = "المحاسبة",
            Icon = "FolderTree",
            EntityId = a.Id,
            Code = a.Code,
            DisplayName = a.NameAr,
            SubInfo = a.ParentId.HasValue
                ? $"تحت {a.ParentCode} · {a.ParentNameAr} (مستوى {a.Level})"
                : $"حساب جذر (مستوى {a.Level})",
            DeletedAt = a.DeletedAt,
            DeletedBy = a.DeletedBy,
            CanRestore = !a.ParentIsDeleted,
            CannotRestoreReason = a.ParentIsDeleted
                ? $"الأب \"{a.ParentNameAr}\" ({a.ParentCode}) ما زال في السلة — استعده أولاً"
                : null,
        }).ToList();
    }

    public Task<Result> RestoreAsync(int id, CancellationToken ct)
        => _mediator.Send(new RestoreAccountCommand(id), ct);

    public Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct)
        => _mediator.Send(new PermanentlyDeleteAccountCommand(id), ct);
}
