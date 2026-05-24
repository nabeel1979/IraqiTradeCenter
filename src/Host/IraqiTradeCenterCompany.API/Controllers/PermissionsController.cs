using IraqiTradeCenterCompany.API.Auth;
using IraqiTradeCenterCompany.API.Auth.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IraqiTradeCenterCompany.API.Controllers;

/// <summary>
/// إرجاع كاتالوج الصلاحيات المعرَّفة في النظام كشجرة:
/// Module → Resource → [Actions...] — لرسمها في الواجهة كـ tree-view
/// مع select-all عند مستوى الكل / المودول / المورد.
/// </summary>
[ApiController]
[Authorize]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly AuthDbContext _db;

    public PermissionsController(AuthDbContext db) { _db = db; }

    /// <summary>الشجرة الكاملة — قراءتها مسموحة لكل مستخدم مسجَّل (لازمة لعرض الواجهة).</summary>
    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(CancellationToken ct)
    {
        var perms = await _db.Permissions.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync(ct);

        var tree = perms
            .GroupBy(p => p.Module)
            .Select(mg => new
            {
                module     = mg.Key,
                moduleAr   = PermissionRegistry.ModuleLabelsAr.GetValueOrDefault(mg.Key, mg.Key),
                resources  = mg
                    .GroupBy(p => p.Resource)
                    .Select(rg => new
                    {
                        resource = rg.Key,
                        // اسم المورد العربي = الجزء قبل آخر فعل في الـ NameAr (مثلاً "قراءة القيود اليومية" → "القيود اليومية")
                        resourceAr = TrimAction(rg.First().NameAr),
                        actions  = rg.Select(p => new
                        {
                            code     = p.Code,
                            action   = p.Action,
                            actionAr = PermissionRegistry.ActionLabelsAr.GetValueOrDefault(p.Action, p.Action),
                            nameAr   = p.NameAr,
                        }).ToList()
                    }).ToList()
            }).ToList();

        return Ok(new { success = true, data = tree });
    }

    /// <summary>قائمة مسطَّحة (لـ debugging أو استخدامات بسيطة).</summary>
    [HttpGet("flat")]
    public async Task<IActionResult> GetFlat(CancellationToken ct)
    {
        var perms = await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new { p.Code, p.Module, p.Resource, p.Action, p.NameAr })
            .ToListAsync(ct);
        return Ok(new { success = true, data = perms });
    }

    private static string TrimAction(string nameAr)
    {
        // الأسماء من Registry بالشكل "<فعل> <اسم المورد>" → نُسقط الكلمة الأولى
        var idx = nameAr.IndexOf(' ');
        return idx > 0 ? nameAr[(idx + 1)..] : nameAr;
    }
}
