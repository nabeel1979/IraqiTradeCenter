using IraqiTradeCenterCompany.SharedKernel.Interfaces;

namespace IraqiTradeCenterCompany.API.Extensions;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime BaghdadNow
    {
        get
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Arabic Standard Time")); }
            catch { return DateTime.UtcNow.AddHours(3); }
        }
    }
}
