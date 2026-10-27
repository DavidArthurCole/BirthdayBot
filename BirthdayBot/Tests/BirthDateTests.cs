using BirthdayBot.Time;

namespace BirthdayBot.Tests;

public sealed class BirthDateTests {
    private const int CurrentYear = 2026;

    [Fact]
    public void Rejects_february_30() {
        Assert.False(BirthDate.TryCreate(2, 30, null, CurrentYear, out _, out var error));
        Assert.Equal("Day must be between 1 and 29 for February.", error);
    }

    [Fact]
    public void Rejects_april_31() {
        Assert.False(BirthDate.TryCreate(4, 31, null, CurrentYear, out _, out var error));
        Assert.Equal("Day must be between 1 and 30 for April.", error);
    }

    [Fact]
    public void Accepts_february_29_without_year() {
        Assert.True(BirthDate.TryCreate(2, 29, null, CurrentYear, out var date, out var error));
        Assert.Equal(new BirthDate(2, 29, null), date);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Rejects_february_29_with_2023() {
        Assert.False(BirthDate.TryCreate(2, 29, 2023, CurrentYear, out _, out var error));
        Assert.Equal("February 29 only exists in leap years; 2023 is not one.", error);
    }

    [Fact]
    public void Rejects_year_after_current() {
        Assert.False(BirthDate.TryCreate(3, 4, CurrentYear + 1, CurrentYear, out _, out var error));
        Assert.Equal("Year must be between 1900 and 2026.", error);
    }

    [Fact]
    public void Rejects_year_before_1900() {
        Assert.False(BirthDate.TryCreate(3, 4, 1899, CurrentYear, out _, out var error));
        Assert.Equal("Year must be between 1900 and 2026.", error);
    }

    [Fact]
    public void Occurrence_of_feb_29_in_2025_is_feb_28() {
        Assert.Equal(new DateOnly(2025, 2, 28), new BirthDate(2, 29, null).OccurrenceIn(2025));
    }

    [Fact]
    public void Occurrence_of_feb_29_in_2024_is_feb_29() {
        Assert.Equal(new DateOnly(2024, 2, 29), new BirthDate(2, 29, null).OccurrenceIn(2024));
    }

    [Fact]
    public void Age_is_null_without_year() {
        Assert.Null(new BirthDate(3, 4, null).AgeOn(new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public void Age_on_birthday_is_difference_of_years() {
        Assert.Equal(30, new BirthDate(3, 4, 1996).AgeOn(new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public void Format_with_and_without_year() {
        Assert.Equal("March 4", new BirthDate(3, 4, null).Format());
        Assert.Equal("March 4, 1996", new BirthDate(3, 4, 1996).Format());
    }
}
