using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

public sealed class BirthdayForm(
    BirthdayStore birthdays,
    GuildSettingsStore settings,
    TimeProvider time,
    ILogger<BirthdayForm> logger) {
    public const string ModalId = "bday:form";
    private const string MonthId = "month";
    private const string DayId = "day";
    private const string YearId = "year";

    public static Modal Build(BirthDate? current) {
        var month = new SelectMenuBuilder()
            .WithCustomId(MonthId)
            .WithPlaceholder("Pick a month")
            .WithMinValues(1)
            .WithMaxValues(1);
        for (var m = 1; m <= 12; m++) {
            var name = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m);
            month.AddOption(name, m.ToString(CultureInfo.InvariantCulture), null, null, current?.Month == m);
        }
        var day = new TextInputBuilder()
            .WithCustomId(DayId)
            .WithStyle(TextInputStyle.Short)
            .WithPlaceholder("1 to 31")
            .WithMinLength(1)
            .WithMaxLength(2)
            .WithRequired(true);
        if (current is { } date) day.WithValue(date.Day.ToString(CultureInfo.InvariantCulture));
        var year = new TextInputBuilder()
            .WithCustomId(YearId)
            .WithStyle(TextInputStyle.Short)
            .WithPlaceholder("e.g. 1996")
            .WithMinLength(4)
            .WithMaxLength(4)
            .WithRequired(false);
        if (current?.Year is { } y) year.WithValue(y.ToString(CultureInfo.InvariantCulture));
        return new ModalBuilder()
            .WithTitle("Your birthday")
            .WithCustomId(ModalId)
            .AddLabel(new LabelBuilder("Month", month, null, null))
            .AddLabel(new LabelBuilder("Day", day, null, null))
            .AddLabel(new LabelBuilder("Year", year, "Optional. Leave it blank to keep your age private.", null))
            .Build();
    }

    public async Task HandleAsync(SocketModal modal) {
        if (modal.GuildId is not { } guildId) return;
        var row = await birthdays.GetAsync(guildId, modal.User.Id, CancellationToken.None);
        var zone = row is null ? null : TimeZoneCatalog.Resolve(row.TimeZone);
        if (row is null || zone is null) {
            await Cards.ReplyAsync(modal, Cards.Notice(Cards.Danger, "Pick a time zone first", "Run `/birthday set` and choose your time zone from the list."));
            return;
        }
        var now = time.GetUtcNow();
        if (!TryRead(modal, LocalClock.Today(now, zone).Year, out var birthday, out var error)) {
            await Cards.ReplyAsync(modal, Cards.Notice(Cards.Danger, "That date doesn't work", error, Cards.Button("Try again", BirthdayCards.EditId, ButtonStyle.Primary)));
            return;
        }
        await birthdays.SetBirthdayAsync(guildId, modal.User.Id, birthday, now, CancellationToken.None);
        logger.LogInformation("User {UserId} set birthday in guild {GuildId}", modal.User.Id, guildId);
        var feed = await settings.GetFeedChannelAsync(guildId, CancellationToken.None);
        await Cards.ReplyAsync(modal, BirthdayCards.Profile(modal.User, true, row, birthday, now, feed is not null));
    }

    private static bool TryRead(SocketModal modal, int currentYear, out BirthDate birthday, out string error) {
        birthday = default;
        string? Value(string id) => modal.Data.Components.FirstOrDefault(c => c.CustomId == id) is { } component
            ? component.Values?.FirstOrDefault() ?? component.Value
            : null;
        if (!int.TryParse(Value(MonthId), CultureInfo.InvariantCulture, out var month)) {
            error = "Pick a month from the list.";
            return false;
        }
        if (!int.TryParse(Value(DayId)?.Trim(), CultureInfo.InvariantCulture, out var day)) {
            error = "The day has to be a number, like `14`.";
            return false;
        }
        int? year = null;
        var yearText = Value(YearId)?.Trim();
        if (!string.IsNullOrEmpty(yearText)) {
            if (!int.TryParse(yearText, CultureInfo.InvariantCulture, out var parsed)) {
                error = "The year has to be four digits, like `1996`, or left blank.";
                return false;
            }
            year = parsed;
        }
        return BirthDate.TryCreate(month, day, year, currentYear, out birthday, out error);
    }
}
