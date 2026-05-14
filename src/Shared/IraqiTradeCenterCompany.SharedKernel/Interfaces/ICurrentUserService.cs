namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? FullName { get; }
    int CompanyId { get; }
    int? SalesRepId { get; }
    bool IsAuthenticated { get; }
}
