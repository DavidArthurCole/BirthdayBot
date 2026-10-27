using BirthdayBot.Storage;

namespace BirthdayBot.Scheduling;

public sealed record DueBirthday(ulong GuildId, ulong UserId, DateOnly LocalDate, int? Age);

public static class DueBirthdays {
    public static IReadOnlyList<(int Month, int Day)> CandidateMonthDays(DateTimeOffset utcNow) {
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        var days = new List<(int Month, int Day)> { MonthDay(today.AddDays(-1)), MonthDay(today), MonthDay(today.AddDays(1)) };
        if (days.Contains((2, 28)) && !DateTime.IsLeapYear(today.Year)) days.Add((2, 29));
        return days;
    }

    public static IReadOnlyList<DueBirthday> Select(IEnumerable<BirthdayRow> rows, DateTimeOffset utcNow, Func<string, TimeZoneInfo?> resolve) {
        var due = new List<DueBirthday>();
        foreach (var row in rows) {
            if (row.Birthday is not { } birthday) continue;
            var zone = resolve(row.TimeZone);
            if (zone is null) continue;
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
            if (localDate != birthday.OccurrenceIn(localDate.Year)) continue;
            due.Add(new DueBirthday(row.GuildId, row.UserId, localDate, birthday.AgeOn(localDate)));
        }
        return due;
    }

    private static (int Month, int Day) MonthDay(DateOnly date) => (date.Month, date.Day);
}
