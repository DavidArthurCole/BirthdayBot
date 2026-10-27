using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

[CommandContextType(InteractionContextType.Guild)]
public sealed class FeedModule(
    GuildSettingsStore settings,
    TimeProvider time,
    ILogger<FeedModule> logger) : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("birthday-feed", "Set up birthday announcements and messages")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public async Task OpenAsync() => await Cards.ReplyAsync(Context.Interaction, await PanelAsync(null));

    [ComponentInteraction(BirthdayCards.FeedChannelId, true)]
    public async Task ChannelSelectedAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!FeedPermissions.CanManage(Context.User)) {
            await Cards.ReplyAsync(component, BirthdayCards.AdminsOnly());
            return;
        }
        if (!ulong.TryParse(component.Data.Values.FirstOrDefault(), CultureInfo.InvariantCulture, out var id) || Context.Guild.GetChannel(id) is not { } channel) {
            await Cards.UpdateAsync(component, await PanelAsync(null));
            return;
        }
        if (FeedPermissions.Missing(Context.Guild, channel) is { } missing) {
            await Cards.UpdateAsync(component, await PanelAsync($"I can't post in <#{id}> because I'm missing **{missing}** there. Grant it, or pick another channel."));
            return;
        }
        await settings.SetFeedChannelAsync(Context.Guild.Id, id, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} feed channel set to {ChannelId}", Context.Guild.Id, id);
        await Cards.UpdateAsync(component, await PanelAsync(null));
    }

    [ComponentInteraction(BirthdayCards.FeedOffId, true)]
    public async Task TurnOffAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!FeedPermissions.CanManage(Context.User)) {
            await Cards.ReplyAsync(component, BirthdayCards.AdminsOnly());
            return;
        }
        await settings.SetFeedChannelAsync(Context.Guild.Id, null, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} feed channel cleared", Context.Guild.Id);
        await Cards.UpdateAsync(component, await PanelAsync(null));
    }

    [ComponentInteraction(BirthdayCards.FeedEveryoneId, true)]
    public async Task ToggleEveryoneAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!FeedPermissions.CanManage(Context.User)) {
            await Cards.ReplyAsync(component, BirthdayCards.AdminsOnly());
            return;
        }
        var current = await settings.GetAsync(Context.Guild.Id, CancellationToken.None);
        await settings.SetPingEveryoneAsync(Context.Guild.Id, !current.PingEveryone, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} ping everyone set to {PingEveryone}", Context.Guild.Id, !current.PingEveryone);
        await Cards.UpdateAsync(component, await PanelAsync(null));
    }

    private async Task<MessageComponent> PanelAsync(string? attemptProblem) {
        var saved = await settings.GetAsync(Context.Guild.Id, CancellationToken.None);
        if (attemptProblem is not null || saved.FeedChannelId is not { } id) return BirthdayCards.FeedPanel(saved, attemptProblem);
        if (Context.Guild.GetChannel(id) is not { } channel) {
            return BirthdayCards.FeedPanel(saved, "That channel no longer exists, so nothing is being announced. Pick a new one.");
        }
        if (FeedPermissions.Missing(Context.Guild, channel) is { } missing) {
            return BirthdayCards.FeedPanel(saved, $"I'm missing **{missing}** in <#{id}>, so pings there will fail.");
        }
        if (saved.PingEveryone && !FeedPermissions.CanMentionEveryone(Context.Guild, channel)) {
            return BirthdayCards.FeedPanel(saved, $"I'm missing **Mention @everyone** in <#{id}>, so posts there only ping the birthday person.");
        }
        return BirthdayCards.FeedPanel(saved, null);
    }
}
