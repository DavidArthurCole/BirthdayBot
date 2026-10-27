using BirthdayBot.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BirthdayBot.Tests;

public sealed class MigratorTests {
    [Fact]
    public async Task Applies_twice_without_error() {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString)) {
            return;
        }
        var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await Migrator.ApplyAsync(dataSource, migrationsDirectory, NullLogger.Instance, CancellationToken.None);
        await Migrator.ApplyAsync(dataSource, migrationsDirectory, NullLogger.Instance, CancellationToken.None);
        await using var count = dataSource.CreateCommand("select count(*) from schema_migrations");
        var applied = Assert.IsType<long>(await count.ExecuteScalarAsync());
        Assert.Equal(Directory.GetFiles(migrationsDirectory, "*.up.sql").LongLength, applied);
    }
}
