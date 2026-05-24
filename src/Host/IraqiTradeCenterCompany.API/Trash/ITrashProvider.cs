using IraqiTradeCenterCompany.SharedKernel.Models;

namespace IraqiTradeCenterCompany.API.Trash;

/// <summary>
/// مُزوِّد سلّة لنوع كيان واحد. كل نوع له تنفيذ مستقل (Provider) يُسجَّل في DI
/// ويوفّر القراءة والاستعادة والحذف النهائي للسجلات المحذوفة ناعماً من ذلك النوع.
/// تُجمّع كل المُزوِّدين معاً في <c>ITrashService</c> ليكوّنوا السلة الموحَّدة.
/// </summary>
public interface ITrashProvider
{
    /// <summary>المعرّف التقني للنوع — يُستخدم كـ identifier في URLs (case-sensitive).</summary>
    string EntityType { get; }

    /// <summary>قائمة السجلات المحذوفة ناعماً من هذا النوع.</summary>
    Task<List<TrashItemDto>> ListAsync(CancellationToken ct);

    /// <summary>استعادة سجل واحد من السلة (يعكس الحذف الناعم).</summary>
    Task<Result> RestoreAsync(int id, CancellationToken ct);

    /// <summary>حذف نهائي للسجل — مسح فعلي من DB، لا يمكن التراجع عنه.</summary>
    Task<Result> PermanentlyDeleteAsync(int id, CancellationToken ct);
}
