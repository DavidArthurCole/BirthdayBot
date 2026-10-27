using Npgsql;

namespace BirthdayBot.Storage;

public sealed class GuildSettingsStore(NpgsqlDataSource dataSource) {
    public async Task<ulong?> GetFeedChannelAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("select feed_channel_id from guild_settings where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        var value = await command.ExecuteScalarAsync(ct);
        return value is long channelId ? unchecked((ulong)channelId) : null;
    }

    public async Task SetFeedChannelAsync(ulong guildId, ulong? channelId, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "insert into guild_settings (guild_id, feed_channel_id, updated_at) values ($1, $2, $3) " +
            "on conflict (guild_id) do update set feed_channel_id = excluded.feed_channel_id, updated_at = excluded.updated_at");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(channelId is { } channel ? (object)unchecked((long)channel) : DBNull.Value);
        command.Parameters.AddWithValue(now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteGuildAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from guild_settings where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        await command.ExecuteNonQueryAsync(ct);
    }
}
