using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

[Group("birthday", "Set and view birthdays in this server")]
[CommandContextType(InteractionContextType.Guild)]
public sealed class BirthdayModule(
    BirthdayStore birthdays,
    GuildSettingsStore settings,
    TimeProvider time,
    ILogger<BirthdayModule> logger) : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("set", "Set your birthday for this server")]
    public async Task SetAsync(
        [Choice("January", 1)]
        [Choice("February", 2)]
        [Choice("March", 3)]
        [Choice("April", 4)]
        [Choice("May", 5)]
        [Choice("June", 6)]
        [Choice("July", 7)]
        [Choice("August", 8)]
        [Choice("September", 9)]
        [Choice("October", 10)]
        [Choice("November", 11)]
        [Choice("December", 12)]
        int month,
        [MinValue(1)] [MaxValue(31)] int day,
        int? year = null) {
        var row = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        var zone = row is null ? null : TimeZoneCatalog.Resolve(row.TimeZone);
        if (zone is null) {
            await RespondAsync("Set your time zone first with /timezone set.", ephemeral: true);
            return;
        }
        var currentYear = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).Year;
        if (!BirthDate.TryCreate(month, day, year, currentYear, out var date, out var error)) {
            await RespondAsync(error, ephemeral: true);
            return;
        }
        var stored = await birthdays.SetBirthdayAsync(Context.Guild.Id, Context.User.Id, date, time.GetUtcNow(), CancellationToken.None);
        if (!stored) {
            await RespondAsync("Set your time zone first with /timezone set.", ephemeral: true);
            return;
        }
        logger.LogInformation("User {UserId} set birthday in guild {GuildId}", Context.User.Id, Context.Guild.Id);
        var reply = $"Birthday set to {date.Format()}.";
        var feed = await settings.GetFeedChannelAsync(Context.Guild.Id, CancellationToken.None);
        if (feed is null) reply += " This server has no birthday feed configured yet.";
        await RespondAsync(reply, ephemeral: true);
    }

    [SlashCommand("show", "Show a member's birthday in this server")]
    public async Task ShowAsync(SocketGuildUser? user = null) {
        var target = user ?? Context.User;
        var row = await birthdays.GetAsync(Context.Guild.Id, target.Id, CancellationToken.None);
        if (row?.Birthday is not { } birthday) {
            await RespondAsync("No birthday set.", ephemeral: true);
            return;
        }
        var reply = birthday.Format();
        var zone = TimeZoneCatalog.Resolve(row.TimeZone) ?? TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
        if (birthday.AgeOn(today) is { } age) reply += $" (age {age})";
        await RespondAsync(reply, ephemeral: true);
    }

    [SlashCommand("remove", "Remove your birthday from this server")]
    public async Task RemoveAsync() {
        var removed = await birthdays.RemoveAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        await RespondAsync(removed ? "Removed." : "You had no birthday set here.", ephemeral: true);
    }

    [SlashCommand("upcoming", "Show the next ten birthdays in this server")]
    public async Task UpcomingAsync() {
        var rows = await birthdays.ListGuildAsync(Context.Guild.Id, CancellationToken.None);
        var caller = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        var zone = caller is null ? TimeZoneInfo.Utc : TimeZoneCatalog.Resolve(caller.TimeZone) ?? TimeZoneInfo.Utc;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).DateTime);
        var upcoming = rows
            .Where(r => r.Birthday is not null)
            .Select(r => {
                var birthday = r.Birthday.GetValueOrDefault();
                var occurrence = birthday.OccurrenceIn(today.Year);
                if (occurrence < today) occurrence = birthday.OccurrenceIn(today.Year + 1);
                return (Row: r, Birthday: birthday, Days: occurrence.DayNumber - today.DayNumber);
            })
            .OrderBy(x => x.Days)
            .ThenBy(x => x.Row.UserId)
            .Take(10)
            .ToList();
        if (upcoming.Count == 0) {
            await RespondAsync("No birthdays set in this server yet.", ephemeral: true);
            return;
        }
        var lines = upcoming.Select(x => {
            var name = Context.Guild.GetUser(x.Row.UserId)?.DisplayName ?? MentionUtils.MentionUser(x.Row.UserId);
            var when = x.Days == 0 ? "today" : $"in {x.Days} days";
            return $"{name} - {(x.Birthday with { Year = null }).Format()} ({when})";
        });
        await RespondAsync(string.Join("\n", lines), ephemeral: true);
    }
}
