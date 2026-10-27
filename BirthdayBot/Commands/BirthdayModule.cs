using BirthdayBot.Discord;
using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

[Group("birthday", "Save your birthday and see who is celebrating")]
[CommandContextType(InteractionContextType.Guild)]
public sealed class BirthdayModule(
    BirthdayStore birthdays,
    GuildSettingsStore settings,
    BirthdayMessageStore messages,
    TimeProvider time,
    ILogger<BirthdayModule> logger) : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("set", "Save or change your birthday")]
    public async Task SetAsync(
        [Summary("timezone", "Your time zone, so the ping lands at your midnight")]
        [Autocomplete(typeof(TimeZoneAutocompleteHandler))]
        string timezone) {
        if (TimeZoneCatalog.Resolve(timezone) is null) {
            await Cards.ReplyAsync(Context.Interaction, Cards.Notice(
                Cards.Danger,
                "Unknown time zone",
                $"`{timezone}` isn't a time zone I know. Start typing a city, like `New York` or `Berlin`, and pick one from the list."));
            return;
        }
        await birthdays.SetTimeZoneAsync(Context.Guild.Id, Context.User.Id, timezone, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("User {UserId} set time zone in guild {GuildId}", Context.User.Id, Context.Guild.Id);
        var row = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        await RespondWithModalAsync(BirthdayForm.Build(row?.Birthday));
    }

    [SlashCommand("view", "See a member's birthday")]
    public async Task ViewAsync([Summary("member", "Leave empty to see your own")] SocketGuildUser? member = null) {
        IUser target = member ?? Context.User;
        var own = target.Id == Context.User.Id;
        var row = await birthdays.GetAsync(Context.Guild.Id, target.Id, CancellationToken.None);
        if (row?.Birthday is not { } birthday) {
            await Cards.ReplyAsync(Context.Interaction, BirthdayCards.NotSaved(target, own));
            return;
        }
        var feed = await settings.GetFeedChannelAsync(Context.Guild.Id, CancellationToken.None);
        ProfileAdmin? admin = null;
        if (!own && FeedPermissions.CanManage(Context.User)) {
            admin = new ProfileAdmin(await messages.GetAsync(Context.Guild.Id, target.Id, CancellationToken.None));
        }
        await Cards.ReplyAsync(Context.Interaction, BirthdayCards.Profile(target, own, row, birthday, time.GetUtcNow(), feed is not null, admin));
    }

    [SlashCommand("upcoming", "See the next birthdays in this server")]
    public async Task UpcomingAsync() {
        var rows = await birthdays.ListGuildAsync(Context.Guild.Id, CancellationToken.None);
        var now = time.GetUtcNow();
        var entries = new List<UpcomingEntry>(rows.Count);
        foreach (var row in rows) {
            if (row.Birthday is not { } birthday) continue;
            var zone = TimeZoneCatalog.Resolve(row.TimeZone) ?? TimeZoneInfo.Utc;
            var today = LocalClock.Today(now, zone);
            var next = birthday.NextOccurrence(today);
            entries.Add(new UpcomingEntry(row.UserId, birthday, LocalClock.MidnightUtc(next, zone), birthday.AgeOn(next), next == today));
        }
        var upcoming = entries
            .OrderByDescending(e => e.IsToday)
            .ThenBy(e => e.At)
            .ThenBy(e => e.UserId)
            .Take(10)
            .ToList();
        await Cards.ReplyAsync(Context.Interaction, BirthdayCards.Upcoming(upcoming, entries.Count));
    }

    [SlashCommand("remove", "Remove your birthday from this server")]
    public async Task RemoveAsync() {
        var row = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        if (row is null) {
            await Cards.ReplyAsync(Context.Interaction, BirthdayCards.NotSaved(Context.User, true));
            return;
        }
        await Cards.ReplyAsync(Context.Interaction, BirthdayCards.ConfirmRemove());
    }

    [ComponentInteraction(BirthdayCards.EditId, true)]
    public async Task EditAsync() {
        var row = await birthdays.GetAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        if (row is null) {
            await Cards.ReplyAsync(Context.Interaction, Cards.Notice(Cards.Danger, "Pick a time zone first", "Run `/birthday set` and choose your time zone from the list."));
            return;
        }
        await RespondWithModalAsync(BirthdayForm.Build(row.Birthday));
    }

    [ComponentInteraction(BirthdayCards.RemoveId, true)]
    public async Task ConfirmRemoveAsync() {
        await birthdays.RemoveAsync(Context.Guild.Id, Context.User.Id, CancellationToken.None);
        logger.LogInformation("User {UserId} removed birthday in guild {GuildId}", Context.User.Id, Context.Guild.Id);
        await Cards.UpdateAsync((SocketMessageComponent)Context.Interaction, BirthdayCards.Removed());
    }

    [ComponentInteraction(BirthdayCards.CancelId, true)]
    public async Task CancelRemoveAsync() =>
        await Cards.UpdateAsync((SocketMessageComponent)Context.Interaction, Cards.Notice(Cards.Neutral, "Nothing changed", "Your birthday is still saved."));
}
