using Npgsql;

namespace BirthdayBot.Storage;

public sealed record GuildSettings(ulong? FeedChannelId, bool PingEveryone) {
    public static readonly GuildSettings Default = new(null, true);
}

public sealed class GuildSettingsStore(NpgsqlDataSource dataSource) {
    public async Task<GuildSettings> GetAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("select feed_channel_id, ping_everyone from guild_settings where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return GuildSettings.Default;
        ulong? channelId = reader.IsDBNull(0) ? null : unchecked((ulong)reader.GetInt64(0));
        return new GuildSettings(channelId, reader.GetBoolean(1));
    }

    public async Task<ulong?> GetFeedChannelAsync(ulong guildId, CancellationToken ct) => (await GetAsync(guildId, ct)).FeedChannelId;

    public async Task SetFeedChannelAsync(ulong guildId, ulong? channelId, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "insert into guild_settings (guild_id, feed_channel_id, updated_at) values ($1, $2, $3) " +
            "on conflict (guild_id) do update set feed_channel_id = excluded.feed_channel_id, updated_at = excluded.updated_at");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(channelId is { } channel ? (object)unchecked((long)channel) : DBNull.Value);
        command.Parameters.AddWithValue(now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SetPingEveryoneAsync(ulong guildId, bool pingEveryone, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "insert into guild_settings (guild_id, ping_everyone, updated_at) values ($1, $2, $3) " +
            "on conflict (guild_id) do update set ping_everyone = excluded.ping_everyone, updated_at = excluded.updated_at");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(pingEveryone);
        command.Parameters.AddWithValue(now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteGuildAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from guild_settings where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        await command.ExecuteNonQueryAsync(ct);
    }
}
