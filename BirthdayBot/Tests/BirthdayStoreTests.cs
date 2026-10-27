using BirthdayBot.Storage;
using BirthdayBot.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace BirthdayBot.Tests;

public sealed class BirthdayStoreTests {
    private const ulong GuildId = 18446744073709551000UL;
    private const ulong UserId = 18446744073709551001UL;

    [Fact]
    public async Task Set_time_zone_keeps_birthday() {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString)) {
            return;
        }
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await Migrator.ApplyAsync(dataSource, Path.Combine(AppContext.BaseDirectory, "Migrations"), NullLogger.Instance, CancellationToken.None);
        var store = new BirthdayStore(dataSource);
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        await store.DeleteGuildAsync(GuildId, CancellationToken.None);
        try {
            await store.SetTimeZoneAsync(GuildId, UserId, "America/New_York", now, CancellationToken.None);
            Assert.True(await store.SetBirthdayAsync(GuildId, UserId, new BirthDate(3, 4, 1996), now, CancellationToken.None));
            await store.SetTimeZoneAsync(GuildId, UserId, "Europe/London", now.AddMinutes(1), CancellationToken.None);
            var row = await store.GetAsync(GuildId, UserId, CancellationToken.None);
            Assert.NotNull(row);
            Assert.Equal(new BirthDate(3, 4, 1996), row.Birthday);
            Assert.Equal("Europe/London", row.TimeZone);
        } finally {
            await store.DeleteGuildAsync(GuildId, CancellationToken.None);
        }
    }
}
