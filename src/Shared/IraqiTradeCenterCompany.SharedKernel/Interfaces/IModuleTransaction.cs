namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

/// <summary>
/// مُغلّف للتعامل مع المعاملات عبر عدة مودولز (DbContexts متعددة)
/// </summary>
public interface IModuleTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IModuleTransactionFactory
{
    Task<IModuleTransaction> BeginAsync(CancellationToken ct = default);
}
