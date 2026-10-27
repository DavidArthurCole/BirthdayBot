using System.Globalization;
using BirthdayBot.Time;
using Npgsql;

namespace BirthdayBot.Storage;

public sealed record BirthdayRow(ulong GuildId, ulong UserId, BirthDate? Birthday, string TimeZone);

public sealed class BirthdayStore(NpgsqlDataSource dataSource) {
    private const string Columns = "guild_id, user_id, month, day, year, time_zone";

    public async Task<BirthdayRow?> GetAsync(ulong guildId, ulong userId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand($"select {Columns} from birthdays where guild_id = $1 and user_id = $2");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        var rows = await ReadRowsAsync(command, ct);
        return rows.Count == 0 ? null : rows[0];
    }

    public async Task SetTimeZoneAsync(ulong guildId, ulong userId, string timeZone, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "insert into birthdays (guild_id, user_id, time_zone, updated_at) values ($1, $2, $3, $4) " +
            "on conflict (guild_id, user_id) do update set time_zone = excluded.time_zone, updated_at = excluded.updated_at");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        command.Parameters.AddWithValue(timeZone);
        command.Parameters.AddWithValue(now.ToUniversalTime());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> SetBirthdayAsync(ulong guildId, ulong userId, BirthDate birthday, DateTimeOffset now, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            "update birthdays set month = $3, day = $4, year = $5, updated_at = $6 where guild_id = $1 and user_id = $2");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        command.Parameters.AddWithValue((short)birthday.Month);
        command.Parameters.AddWithValue((short)birthday.Day);
        command.Parameters.AddWithValue(birthday.Year is { } year ? (object)(short)year : DBNull.Value);
        command.Parameters.AddWithValue(now.ToUniversalTime());
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> RemoveAsync(ulong guildId, ulong userId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from birthdays where guild_id = $1 and user_id = $2");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        command.Parameters.AddWithValue(unchecked((long)userId));
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<BirthdayRow>> ListGuildAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand(
            $"select {Columns} from birthdays where guild_id = $1 and month is not null order by month, day, user_id");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        return await ReadRowsAsync(command, ct);
    }

    public async Task<IReadOnlyList<BirthdayRow>> ListCandidatesAsync(IReadOnlyCollection<(int Month, int Day)> monthDays, CancellationToken ct) {
        if (monthDays.Count == 0) {
            return [];
        }
        var tuples = new List<string>(monthDays.Count);
        for (var index = 1; index < monthDays.Count * 2; index += 2) {
            tuples.Add(string.Create(CultureInfo.InvariantCulture, $"(${index}, ${index + 1})"));
        }
        await using var command = dataSource.CreateCommand(
            $"select {Columns} from birthdays where (month, day) in ({string.Join(", ", tuples)})");
        foreach (var (month, day) in monthDays) {
            command.Parameters.AddWithValue((short)month);
            command.Parameters.AddWithValue((short)day);
        }
        return await ReadRowsAsync(command, ct);
    }

    public async Task DeleteGuildAsync(ulong guildId, CancellationToken ct) {
        await using var command = dataSource.CreateCommand("delete from birthdays where guild_id = $1");
        command.Parameters.AddWithValue(unchecked((long)guildId));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<List<BirthdayRow>> ReadRowsAsync(NpgsqlCommand command, CancellationToken ct) {
        List<BirthdayRow> rows = [];
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            rows.Add(ReadRow(reader));
        }
        return rows;
    }

    private static BirthdayRow ReadRow(NpgsqlDataReader reader) {
        var guildId = unchecked((ulong)reader.GetInt64(0));
        var userId = unchecked((ulong)reader.GetInt64(1));
        var timeZone = reader.GetString(5);
        if (reader.IsDBNull(2)) {
            return new BirthdayRow(guildId, userId, null, timeZone);
        }
        int? year = reader.IsDBNull(4) ? null : reader.GetInt16(4);
        return new BirthdayRow(guildId, userId, new BirthDate(reader.GetInt16(2), reader.GetInt16(3), year), timeZone);
    }
}
