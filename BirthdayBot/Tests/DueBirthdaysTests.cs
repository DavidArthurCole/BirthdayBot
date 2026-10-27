using BirthdayBot.Scheduling;
using BirthdayBot.Storage;
using BirthdayBot.Time;

namespace BirthdayBot.Tests;

public sealed class DueBirthdaysTests {
    private const ulong Guild = 1;
    private const string Kiritimati = "Pacific/Kiritimati";
    private const string PagoPago = "Pacific/Pago_Pago";
    private const string NewYork = "America/New_York";
    private const string Utc = "UTC";

    private static TimeZoneInfo? Resolve(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

    private static BirthdayRow Row(ulong userId, BirthDate? birthday, string zone) => new(Guild, userId, birthday, zone);

    private static IReadOnlyList<DueBirthday> Select(DateTimeOffset utcNow, params BirthdayRow[] rows) => DueBirthdays.Select(rows, utcNow, Resolve);

    [Fact]
    public void Kiritimati_and_pago_pago_become_due_at_different_instants() {
        var rows = new[] { Row(10, new BirthDate(3, 4, null), Kiritimati), Row(20, new BirthDate(3, 4, null), PagoPago) };
        var east = Select(new DateTimeOffset(2026, 3, 3, 10, 30, 0, TimeSpan.Zero), rows);
        var west = Select(new DateTimeOffset(2026, 3, 4, 11, 30, 0, TimeSpan.Zero), rows);
        Assert.Equal([10ul], east.Select(d => d.UserId));
        Assert.Equal([20ul], west.Select(d => d.UserId));
    }

    [Fact]
    public void Not_due_one_minute_before_local_midnight() {
        var due = Select(new DateTimeOffset(2025, 3, 4, 4, 59, 0, TimeSpan.Zero), Row(10, new BirthDate(3, 4, null), NewYork));
        Assert.Empty(due);
    }

    [Fact]
    public void Due_one_minute_after_local_midnight() {
        var due = Select(new DateTimeOffset(2025, 3, 4, 5, 1, 0, TimeSpan.Zero), Row(10, new BirthDate(3, 4, null), NewYork));
        var single = Assert.Single(due);
        Assert.Equal(new DateOnly(2025, 3, 4), single.LocalDate);
    }

    [Fact]
    public void Feb_29_row_due_on_feb_28_in_2025() {
        var row = Row(10, new BirthDate(2, 29, null), Utc);
        var due = Select(new DateTimeOffset(2025, 2, 28, 12, 0, 0, TimeSpan.Zero), row);
        var single = Assert.Single(due);
        Assert.Equal(new DateOnly(2025, 2, 28), single.LocalDate);
    }

    [Fact]
    public void Feb_29_row_due_on_feb_29_in_2024() {
        var row = Row(10, new BirthDate(2, 29, null), Utc);
        Assert.Empty(Select(new DateTimeOffset(2024, 2, 28, 12, 0, 0, TimeSpan.Zero), row));
        var single = Assert.Single(Select(new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero), row));
        Assert.Equal(new DateOnly(2024, 2, 29), single.LocalDate);
    }

    [Fact]
    public void Age_is_local_year_minus_birth_year() {
        var due = Select(new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero), Row(10, new BirthDate(3, 4, 1996), Utc));
        Assert.Equal(30, Assert.Single(due).Age);
    }

    [Fact]
    public void Age_null_without_year() {
        var due = Select(new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero), Row(10, new BirthDate(3, 4, null), Utc));
        Assert.Null(Assert.Single(due).Age);
    }

    [Fact]
    public void Skips_row_without_birthday() {
        var due = Select(new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero), Row(10, null, Utc));
        Assert.Empty(due);
    }

    [Fact]
    public void Skips_unresolvable_zone() {
        var rows = new[] { Row(10, new BirthDate(3, 4, null), "Nowhere/Invalid") };
        var due = DueBirthdays.Select(rows, new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero), _ => null);
        Assert.Empty(due);
    }

    [Fact]
    public void Candidates_include_feb_29_when_feb_28_present_in_non_leap_year() {
        var candidates = DueBirthdays.CandidateMonthDays(new DateTimeOffset(2025, 2, 28, 12, 0, 0, TimeSpan.Zero));
        Assert.Contains((2, 29), candidates);
        var leap = DueBirthdays.CandidateMonthDays(new DateTimeOffset(2024, 2, 27, 12, 0, 0, TimeSpan.Zero));
        Assert.DoesNotContain((2, 29), leap);
    }

    [Fact]
    public void Candidates_span_three_days_across_year_boundary() {
        var candidates = DueBirthdays.CandidateMonthDays(new DateTimeOffset(2025, 12, 31, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal([(12, 30), (12, 31), (1, 1)], candidates);
    }
}
