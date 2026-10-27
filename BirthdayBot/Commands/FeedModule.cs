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
    [SlashCommand("birthday-feed", "Choose the channel where birthday pings are posted")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public async Task OpenAsync() => await Cards.ReplyAsync(Context.Interaction, await PanelAsync(null));

    [ComponentInteraction(BirthdayCards.FeedChannelId, true)]
    public async Task ChannelSelectedAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!CanManage) {
            await Cards.ReplyAsync(component, AdminsOnly());
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
        await Cards.UpdateAsync(component, BirthdayCards.FeedPanel(id, null));
    }

    [ComponentInteraction(BirthdayCards.FeedOffId, true)]
    public async Task TurnOffAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!CanManage) {
            await Cards.ReplyAsync(component, AdminsOnly());
            return;
        }
        await settings.SetFeedChannelAsync(Context.Guild.Id, null, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} feed channel cleared", Context.Guild.Id);
        await Cards.UpdateAsync(component, BirthdayCards.FeedPanel(null, null));
    }

    private bool CanManage => Context.User is SocketGuildUser { GuildPermissions.ManageGuild: true };

    private static MessageComponent AdminsOnly() =>
        Cards.Notice(Cards.Danger, "Admins only", "You need **Manage Server** to change the birthday channel.");

    private async Task<MessageComponent> PanelAsync(string? attemptProblem) {
        var saved = await settings.GetFeedChannelAsync(Context.Guild.Id, CancellationToken.None);
        if (attemptProblem is not null || saved is not { } id) return BirthdayCards.FeedPanel(saved, attemptProblem);
        if (Context.Guild.GetChannel(id) is not { } channel) {
            return BirthdayCards.FeedPanel(id, "That channel no longer exists, so nothing is being announced. Pick a new one.");
        }
        var missing = FeedPermissions.Missing(Context.Guild, channel);
        return BirthdayCards.FeedPanel(id, missing is null ? null : $"I'm missing **{missing}** in <#{id}>, so pings there will fail.");
    }
}
