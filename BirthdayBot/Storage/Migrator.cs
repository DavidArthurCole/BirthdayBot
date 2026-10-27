using Microsoft.Extensions.Logging;
using Npgsql;

namespace BirthdayBot.Storage;

public static class Migrator {
    private const long LockKey = 727364;

    public static async Task ApplyAsync(NpgsqlDataSource dataSource, string migrationsDirectory, ILogger logger, CancellationToken ct) {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using (var takeLock = new NpgsqlCommand("select pg_advisory_lock($1)", connection)) {
            takeLock.Parameters.AddWithValue(LockKey);
            await takeLock.ExecuteNonQueryAsync(ct);
        }
        try {
            await using (var ledger = new NpgsqlCommand("create table if not exists schema_migrations (name text primary key, applied_at timestamptz not null default now())", connection)) {
                await ledger.ExecuteNonQueryAsync(ct);
            }
            var applied = await ReadAppliedAsync(connection, ct);
            var files = Directory.GetFiles(migrationsDirectory, "*.up.sql").OrderBy(file => Path.GetFileName(file), StringComparer.Ordinal);
            foreach (var file in files) {
                var name = Path.GetFileName(file);
                if (applied.Contains(name)) {
                    continue;
                }
                await ApplyOneAsync(connection, file, name, ct);
                logger.LogInformation("Applied migration {Migration}", name);
            }
        } finally {
            await using var releaseLock = new NpgsqlCommand("select pg_advisory_unlock($1)", connection);
            releaseLock.Parameters.AddWithValue(LockKey);
            await releaseLock.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    private static async Task<HashSet<string>> ReadAppliedAsync(NpgsqlConnection connection, CancellationToken ct) {
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand("select name from schema_migrations", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) {
            applied.Add(reader.GetString(0));
        }
        return applied;
    }

    private static async Task ApplyOneAsync(NpgsqlConnection connection, string file, string name, CancellationToken ct) {
        var sql = await File.ReadAllTextAsync(file, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var migration = new NpgsqlCommand(sql, connection, transaction)) {
            await migration.ExecuteNonQueryAsync(ct);
        }
        await using (var record = new NpgsqlCommand("insert into schema_migrations (name) values ($1)", connection, transaction)) {
            record.Parameters.AddWithValue(name);
            await record.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
