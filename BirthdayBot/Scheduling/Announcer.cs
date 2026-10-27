using System.Net;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Scheduling;

public sealed class Announcer(DiscordSocketClient client, GuildSettingsStore settings, ILogger<Announcer> logger) {
    private const int PerMessage = 10;

    public async Task<bool> PostAsync(ulong guildId, IReadOnlyList<DueBirthday> due, DateTimeOffset now, CancellationToken ct) {
        var channelId = await settings.GetFeedChannelAsync(guildId, ct);
        if (channelId is null) return false;
        if (client.GetChannel(channelId.Value) is not SocketTextChannel channel) {
            logger.LogWarning("Feed channel {ChannelId} for guild {GuildId} no longer exists, clearing it", channelId.Value, guildId);
            await settings.SetFeedChannelAsync(guildId, null, now, ct);
            return false;
        }
        try {
            foreach (var chunk in due.Chunk(PerMessage)) {
                var mentions = new AllowedMentions(AllowedMentionTypes.None) { UserIds = [.. chunk.Select(d => d.UserId)] };
                await channel.SendMessageAsync(components: await BuildAsync(chunk), allowedMentions: mentions, flags: MessageFlags.ComponentsV2);
            }
            return true;
        } catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden) {
            logger.LogWarning(ex, "Missing permission to post birthdays in channel {ChannelId} for guild {GuildId}", channelId.Value, guildId);
            return false;
        }
    }

    private async Task<MessageComponent> BuildAsync(DueBirthday[] chunk) {
        var title = chunk.Length == 1 ? "# \U0001F382 Happy birthday!" : "# \U0001F382 Happy birthday, everyone!";
        var container = new ContainerBuilder()
            .WithAccentColor(Cards.Birthday)
            .WithTextDisplay(title, null);
        foreach (var due in chunk) {
            var line = due.Age is { } age
                ? $"### <@{due.UserId}>\nTurning **{age}** today. Have a great one!"
                : $"### <@{due.UserId}>\nHave a great one!";
            container.AddComponent(Cards.Section(line, await AvatarAsync(due.UserId)));
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
