using System.Globalization;
using Npgsql;

namespace BirthdayBot.Storage;

public sealed class BirthdayMessageStore(NpgsqlDataSource dataSource) {
    public async Task<string?> GetAsync(ulong guildId, ulong userId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("select message from birthday_messages where guild_id = $1 and user_id = $2");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        return await command.ExecuteScalarAsync(ct) as string;
    }

    public async Task<IReadOnlyDictionary<ulong, string>> GetManyAsync(ulong guildId, IReadOnlyCollection<ulong> userIds, CancellationToken ct) {
        Dictionary<ulong, string> messages = [];
        if (userIds.Count == 0) return messages;
        var placeholders = Enumerable.Range(2, userIds.Count).Select(i => string.Create(CultureInfo.InvariantCulture, $"${i}"));
        await using var command = dataSource.CreateCommand(
            $"select user_id, message from birthday_messages where guild_id = $1 and user_id in ({string.Join(", ", placeholders)})");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        foreach (var userId in userIds) command.Parameters.AddWithValue(unchecked((long)userId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) messages[unchecked((ulong)reader.GetInt64(0))] = reader.GetString(1);
        return messages;
    }

    public async Task SetAsync(ulong guildId, ulong userId, string message, ulong updatedBy, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "insert into birthday_messages (guild_id, user_id, message, updated_by, updated_at) values ($1, $2, $3, $4, $5) " +
            "on conflict (guild_id, user_id) do update set message = excluded.message, updated_by = excluded.updated_by, updated_at = excluded.updated_at");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        command.Parameters.AddWithValue(message);
        command.Parameters.AddWithValue(unchecked((long)updatedBy));
        command.Parameters.AddWithValue(now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> ClearAsync(ulong guildId, ulong userId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from birthday_messages where guild_id = $1 and user_id = $2");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task DeleteGuildAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from birthday_messages where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        await command.ExecuteNonQueryAsync(ct);
    }
}
