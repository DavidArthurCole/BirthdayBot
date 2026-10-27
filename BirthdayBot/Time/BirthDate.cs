using System.Globalization;

namespace BirthdayBot.Time;

public readonly record struct BirthDate(int Month, int Day, int? Year) {
    public static bool TryCreate(int month, int day, int? year, int currentYear, out BirthDate date, out string error) {
        date = default;
        error = string.Empty;
        if (month is < 1 or > 12) {
            error = "Month must be between 1 and 12.";
            return false;
        }
        var daysInMonth = DateTime.DaysInMonth(2000, month);
        if (day < 1 || day > daysInMonth) {
            error = string.Create(CultureInfo.InvariantCulture, $"Day must be between 1 and {daysInMonth} for {MonthName(month)}.");
            return false;
        }
        if (year is { } y && (y < 1900 || y > currentYear)) {
            error = string.Create(CultureInfo.InvariantCulture, $"Year must be between 1900 and {currentYear}.");
            return false;
        }
        if (month == 2 && day == 29 && year is { } given && !DateTime.IsLeapYear(given)) {
            error = string.Create(CultureInfo.InvariantCulture, $"February 29 only exists in leap years; {given} is not one.");
            return false;
        }
        date = new BirthDate(month, day, year);
        return true;
    }

    public DateOnly OccurrenceIn(int year) {
        if (Month == 2 && Day == 29 && !DateTime.IsLeapYear(year)) {
            return new DateOnly(year, 2, 28);
        }
        return new DateOnly(year, Month, Day);
    }

    public DateOnly NextOccurrence(DateOnly today) {
        var occurrence = OccurrenceIn(today.Year);
        return occurrence >= today ? occurrence : OccurrenceIn(today.Year + 1);
    }

    public int? AgeOn(DateOnly localDate) => Year is { } year ? localDate.Year - year : null;

    public string Format() {
        var monthDay = string.Create(CultureInfo.InvariantCulture, $"{MonthName(Month)} {Day}");
        return Year is { } year ? string.Create(CultureInfo.InvariantCulture, $"{monthDay}, {year}") : monthDay;
    }

    private static string MonthName(int month) => CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
}
