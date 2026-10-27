using System.Globalization;
using Npgsql;

namespace BirthdayBot.Storage;

public readonly record struct AnnouncementKey(ulong GuildId, ulong UserId, DateOnly LocalDate);

public sealed class AnnouncementLedger(NpgsqlDataSource dataSource) {
    public async Task<IReadOnlySet<AnnouncementKey>> AlreadyPostedAsync(IReadOnlyCollection<AnnouncementKey> keys, CancellationToken ct) {
        HashSet<AnnouncementKey> posted = [];
        if (keys.Count == 0) {
            return posted;
        }
        var tuples = new List<string>(keys.Count);
        for (var index = 1; index < keys.Count * 3; index += 3) {
            tuples.Add(string.Create(CultureInfo.InvariantCulture, $"(${index}, ${index + 1}, ${index + 2})"));
        }
        await using var command = dataSource.CreateCommand(
            $"select guild_id, user_id, local_date from announcements where (guild_id, user_id, local_date) in ({string.Join(", ", tuples)})");
        foreach (var key in keys) {
            command.Parameters.AddWithValue(unchecked((long)key.GuildId));
            command.Parameters.AddWithValue(unchecked((long)key.UserId));
            command.Parameters.AddWithValue(key.LocalDate);
        }
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            posted.Add(new AnnouncementKey(
                unchecked((ulong)reader.GetInt64(0)),
                unchecked((ulong)reader.GetInt64(1)),
                reader.GetFieldValue<DateOnly>(2)));
        }
        return posted;
    }

    public async Task RecordAsync(IReadOnlyCollection<AnnouncementKey> keys, DateTimeOffset postedAt, CancellationToken ct) {
        if (keys.Count == 0) {
            return;
        }
        var tuples = new List<string>(keys.Count);
        for (var index = 2; index < keys.Count * 3 + 1; index += 3) {
            tuples.Add(string.Create(CultureInfo.InvariantCulture, $"(${index}, ${index + 1}, ${index + 2}, $1)"));
        }
        await using var command = dataSource.CreateCommand(
            $"insert into announcements (guild_id, user_id, local_date, posted_at) values {string.Join(", ", tuples)} on conflict do nothing");
        command.Parameters.AddWithValue(postedAt.ToUniversalTime());
        foreach (var key in keys) {
            command.Parameters.AddWithValue(unchecked((long)key.GuildId));
            command.Parameters.AddWithValue(unchecked((long)key.UserId));
            command.Parameters.AddWithValue(key.LocalDate);
        }
        await command.ExecuteNonQueryAsync(ct);
    }
}
