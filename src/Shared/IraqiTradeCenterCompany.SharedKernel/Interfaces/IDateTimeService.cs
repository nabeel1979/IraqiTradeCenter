namespace IraqiTradeCenterCompany.SharedKernel.Interfaces;

public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateTime BaghdadNow { get; }
}
