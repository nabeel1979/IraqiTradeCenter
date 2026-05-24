using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using IraqiTradeCenterCompany.API.Trash;
using Microsoft.AspNetCore.Mvc;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// السلة الموحَّدة لكل النظام — تجمع كل ما هو محذوف ناعماً عبر الكيانات المسجَّلة
/// في <see cref="ITrashService"/>، وتوفّر استعادة وحذف نهائي مع صلاحيات منفصلة:
///   • System.Trash.Read    → استعراض المحتويات
///   • System.Trash.Restore → استعادة عنصر
///   • System.Trash.Purge   → حذف نهائي
/// </summary>
public class TrashController : BaseApiController
{
    private readonly ITrashService _trash;
    public TrashController(ITrashService trash) { _trash = trash; }

    [HttpGet]
    [RequirePermission(PermissionRegistry.System.Trash.Read)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var data = await _trash.ListAllAsync(ct);
        return Ok(new
        {
            success = true,
            data,
            supportedTypes = _trash.SupportedEntityTypes,
        });
    }

    [HttpPost("{entityType}/{id:int}/restore")]
    [RequirePermission(PermissionRegistry.System.Trash.Restore)]
    public async Task<IActionResult> Restore(string entityType, int id, CancellationToken ct)
        => HandleResult(await _trash.RestoreAsync(entityType, id, ct));

    [HttpDelete("{entityType}/{id:int}/permanent")]
    [RequirePermission(PermissionRegistry.System.Trash.Purge)]
    public async Task<IActionResult> Purge(string entityType, int id, CancellationToken ct)
        => HandleResult(await _trash.PermanentlyDeleteAsync(entityType, id, ct));
}
