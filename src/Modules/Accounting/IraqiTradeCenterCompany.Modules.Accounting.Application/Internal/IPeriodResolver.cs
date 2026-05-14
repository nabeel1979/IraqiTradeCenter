namespace IraqiTradeCenterCompany.Modules.Accounting.Application.Internal;

/// <summary>داخلية - تستخدم داخل المودول فقط</summary>
public interface IPeriodResolver
{
    Task<(int FiscalYearId, int PeriodId)> ResolveAsync(DateTime date, CancellationToken ct = default);
}
