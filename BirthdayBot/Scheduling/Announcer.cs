using System.Net;
using BirthdayBot.Storage;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Scheduling;

public sealed class Announcer(DiscordSocketClient client, GuildSettingsStore settings, ILogger<Announcer> logger) {
    public async Task<bool> PostAsync(ulong guildId, IReadOnlyList<DueBirthday> due, DateTimeOffset now, CancellationToken ct) {
        var channelId = await settings.GetFeedChannelAsync(guildId, ct);
        if (channelId is null) return false;
        if (client.GetChannel(channelId.Value) is not SocketTextChannel channel) {
            logger.LogWarning("Feed channel {ChannelId} for guild {GuildId} no longer exists, clearing it", channelId.Value, guildId);
            await settings.SetFeedChannelAsync(guildId, null, now, ct);
            return false;
        }
        var content = string.Join("\n", due.Select(Line));
        var mentions = new AllowedMentions(AllowedMentionTypes.None) { UserIds = [.. due.Select(d => d.UserId)] };
        try {
            await channel.SendMessageAsync(content, allowedMentions: mentions);
            return true;
        } catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Forbidden) {
            logger.LogWarning(ex, "Missing permission to post birthdays in channel {ChannelId} for guild {GuildId}", channelId.Value, guildId);
            return false;
        }
    }

    private static string Line(DueBirthday due) => due.Age is { } age
        ? $"Happy birthday, <@{due.UserId}>! {age} today."
        : $"Happy birthday, <@{due.UserId}>!";
}
