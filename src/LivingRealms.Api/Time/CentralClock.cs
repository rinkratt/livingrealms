namespace LivingRealms.Api.Time;

public static class CentralClock
{
    private static readonly TimeZoneInfo CentralZone = ResolveCentralZone();

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, CentralZone);

    public static DateTimeOffset Convert(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, CentralZone);

    private static TimeZoneInfo ResolveCentralZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }
    }
}
