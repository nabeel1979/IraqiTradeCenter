namespace IraqiTradeCenterCompany.API.Auth.Permissions;

/// <summary>
/// مولِّد صلاحيات أنواع السندات الديناميكية.
/// لكل نوع سند منشأ في acc.JournalVoucherTypes نُولِّد 6 صلاحيات
/// (قراءة/إضافة/تعديل/حذف/طباعة/ترحيل) بصيغة Accounting.Vouchers.{CODE}.{Action}.
/// كل نوع يحصل على Resource مستقل = Vouchers.{CODE} لينعرض كصف منفصل في شجرة الصلاحيات.
/// </summary>
public static class VoucherTypePermissionFactory
{
    /// <summary>
    /// نقطة بداية ترتيب العرض للسندات الديناميكية. تأتي بعد JournalEntries (1100)
    /// وقبل Accounts (1300)، أي ضمن النطاق 1200-1299 (يتسع لـ ~20 نوع).
    /// </summary>
    public const int BaseDisplayOrder = 1200;

    private static readonly string[] AllActions =
    {
        PermissionRegistry.Actions.Read,
        PermissionRegistry.Actions.Create,
        PermissionRegistry.Actions.Update,
        PermissionRegistry.Actions.Delete,
        PermissionRegistry.Actions.Print,
        PermissionRegistry.Actions.Post,
    };

    public record VoucherTypeRef(string Code, string NameAr, int DisplayOrder);

    /// <summary>يُرجع 6 صلاحيات لكل نوع سند.</summary>
    public static IEnumerable<Permission> Build(IEnumerable<VoucherTypeRef> types)
    {
        // ‎نرتّب أولاً ليكون الترتيب في الـ DB متناسقاً مع ترتيب العرض في الواجهة.
        var ordered = types
            .Where(t => !string.IsNullOrWhiteSpace(t.Code))
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var i = 0;
        foreach (var t in ordered)
        {
            var code = t.Code.Trim().ToUpperInvariant();
            // ‎كل نوع يحصل على نطاق 6 لـ DisplayOrder ليبقى تسلسل الإجراءات داخل النوع متماسكاً
            var rowBase = BaseDisplayOrder + i * AllActions.Length;
            for (var k = 0; k < AllActions.Length; k++)
            {
                var act = AllActions[k];
                yield return new Permission
                {
                    Code         = $"{PermissionRegistry.Accounting.Vouchers.Prefix}{code}.{act}",
                    Module       = PermissionRegistry.Modules.Accounting,
                    // ‎Resource مستقل لكل نوع → كل نوع يظهر كصف منفصل في الـ tree.
                    Resource     = $"{PermissionRegistry.Accounting.Vouchers.Resource}.{code}",
                    Action       = act,
                    NameAr       = $"{PermissionRegistry.ActionLabelsAr[act]} {t.NameAr}",
                    DisplayOrder = rowBase + k,
                };
            }
            i++;
        }
    }
}
