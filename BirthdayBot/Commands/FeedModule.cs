using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

[Group("birthday-feed", "Configure the channel where birthday pings are posted")]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[CommandContextType(InteractionContextType.Guild)]
public sealed class FeedModule(
    GuildSettingsStore settings,
    TimeProvider time,
    ILogger<FeedModule> logger) : InteractionModuleBase<SocketInteractionContext> {
    [SlashCommand("set", "Set the birthday feed channel")]
    public async Task SetAsync([ChannelTypes(ChannelType.Text, ChannelType.News)] SocketGuildChannel channel) {
        var missing = FeedPermissions.Missing(Context.Guild, channel);
        if (missing is not null) {
            await RespondAsync($"I am missing {missing} in {MentionUtils.MentionChannel(channel.Id)}.", ephemeral: true);
            return;
        }
        await settings.SetFeedChannelAsync(Context.Guild.Id, channel.Id, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} feed channel set to {ChannelId}", Context.Guild.Id, channel.Id);
        await RespondAsync($"Birthday feed set to {MentionUtils.MentionChannel(channel.Id)}.", ephemeral: true);
    }

    [SlashCommand("clear", "Clear the birthday feed channel")]
    public async Task ClearAsync() {
        await settings.SetFeedChannelAsync(Context.Guild.Id, null, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("Guild {GuildId} feed channel cleared", Context.Guild.Id);
        await RespondAsync("Birthday feed cleared.", ephemeral: true);
    }

    [SlashCommand("show", "Show the birthday feed channel")]
    public async Task ShowAsync() {
        var channelId = await settings.GetFeedChannelAsync(Context.Guild.Id, CancellationToken.None);
        if (channelId is not { } id) {
            await RespondAsync("No feed channel set.", ephemeral: true);
            return;
        }
        var mention = MentionUtils.MentionChannel(id);
        var channel = Context.Guild.GetChannel(id);
        if (channel is null) {
            await RespondAsync($"Birthday feed is {mention}, but that channel no longer exists.", ephemeral: true);
            return;
        }
        var reply = $"Birthday feed is {mention}.";
        var missing = FeedPermissions.Missing(Context.Guild, channel);
        if (missing is not null) reply += $" I am missing {missing} there.";
        await RespondAsync(reply, ephemeral: true);
    }
}
