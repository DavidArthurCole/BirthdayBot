using System.Net;
using BirthdayBot.Commands;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Scheduling;

public sealed class Announcer(
    DiscordSocketClient client,
    GuildSettingsStore settings,
    BirthdayMessageStore messages,
    ILogger<Announcer> logger) {
    private const int PerMessage = 10;

    public async Task<bool> PostAsync(ulong guildId, IReadOnlyList<DueBirthday> due, DateTimeOffset now, CancellationToken ct) {
        var config = await settings.GetAsync(guildId, ct);
        if (config.FeedChannelId is not { } channelId) return false;
        if (client.GetChannel(channelId) is not SocketTextChannel channel) {
            logger.LogWarning("Feed channel {ChannelId} for guild {GuildId} no longer exists, clearing it", channelId, guildId);
            await settings.SetFeedChannelAsync(guildId, null, now, ct);
            return false;
        }
        var custom = await messages.GetManyAsync(guildId, [.. due.Select(d => d.UserId)], ct);
        var everyone = config.PingEveryone && FeedPermissions.CanMentionEveryone(channel.Guild, channel);
        if (config.PingEveryone && !everyone) {
            logger.LogWarning("Guild {GuildId} wants @everyone but the bot lacks Mention Everyone in {ChannelId}", guildId, channelId);
        }
        try {
            foreach (var chunk in due.Chunk(PerMessage)) {
                var mentions = new AllowedMentions(everyone ? AllowedMentionTypes.Everyone : AllowedMentionTypes.None) { UserIds = [.. chunk.Select(d => d.UserId)] };
                await channel.SendMessageAsync(components: await BuildAsync(chunk, custom, everyone), allowedMentions: mentions, flags: MessageFlags.ComponentsV2);
            }
            return true;
        } catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden) {
            logger.LogWarning(ex, "Missing permission to post birthdays in channel {ChannelId} for guild {GuildId}", channelId, guildId);
            return false;
        }
    }

    private async Task<MessageComponent> BuildAsync(DueBirthday[] chunk, IReadOnlyDictionary<ulong, string> custom, bool everyone) {
        var title = chunk.Length == 1 ? "# \U0001F382 Happy birthday!" : "# \U0001F382 Happy birthday, everyone!";
        var container = new ContainerBuilder()
            .WithAccentColor(Cards.Birthday)
            .WithTextDisplay(everyone ? $"@everyone\n{title}" : title, null);
        foreach (var due in chunk) {
            var greeting = due.Age is { } age ? $"Turning **{age}** today." : "It's their birthday today.";
            var body = custom.TryGetValue(due.UserId, out var message) ? BirthdayCards.Quote(message) : "Have a great one!";
            container.AddComponent(Cards.Section($"### <@{due.UserId}>\n{greeting}\n{body}", await AvatarAsync(due.UserId)));
        }
        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    private async Task<string?> AvatarAsync(ulong userId) {
        try {
            var user = await client.GetUserAsync(userId);
            return user?.GetDisplayAvatarUrl(ImageFormat.Auto, 256);
        } catch (HttpException ex) {
            logger.LogWarning(ex, "Could not load avatar for user {UserId}", userId);
            return null;
        }
    }
}
