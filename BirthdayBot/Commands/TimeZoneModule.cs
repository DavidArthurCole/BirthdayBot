using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

[Group("timezone", "Set and view your time zone for this server")]
[CommandContextType(InteractionContextType.Guild)]
public sealed class TimeZoneModule(
    BirthdayStore birthdays,
    TimeProvider time,
    ILogger<TimeZoneModule> logger) : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("set", "Set your IANA time zone for this server")]
    public async Task SetAsync([Autocomplete(typeof(TimeZoneAutocompleteHandler))] string zone) {
        var resolved = TimeZoneCatalog.Resolve(zone);
        if (resolved is null) {
            await RespondAsync("Unknown time zone.", ephemeral: true);
            return;
        }
        await birthdays.SetTimeZoneAsync(Context.Guild.Id, Context.User.Id, zone, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("User {UserId} set time zone in guild {GuildId}", Context.User.Id, Context.Guild.Id);
        await RespondAsync($"Time zone set to {zone}. Your local time is {LocalTime(resolved)}.", ephemeral: true);
    }

    [SlashCommand("show", "Show your stored time zone and local time")]
    public async Task ShowAsync() {
        var row = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        var resolved = row is null ? null : TimeZoneCatalog.Resolve(row.TimeZone);
        if (row is null || resolved is null) {
            await RespondAsync("No time zone set. Use /timezone set.", ephemeral: true);
            return;
        }
        await RespondAsync($"Your time zone is {row.TimeZone}. Your local time is {LocalTime(resolved)}.", ephemeral: true);
    }

    private string LocalTime(TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).ToString("yyyy-MM-dd HH:mm");
}
