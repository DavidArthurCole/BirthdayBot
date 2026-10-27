using System.Globalization;

namespace BirthdayBot.Time;

public static class LocalClock {
    public static DateOnly Today(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);

    public static DateTimeOffset MidnightUtc(DateOnly date, TimeZoneInfo zone) {
        var local = date.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(15);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }

    public static string Offset(DateTimeOffset utcNow, TimeZoneInfo zone) {
        var offset = zone.GetUtcOffset(utcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return string.Create(CultureInfo.InvariantCulture, $"UTC{sign}{offset.Duration():hh\\:mm}");
    }
}
