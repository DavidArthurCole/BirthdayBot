namespace BirthdayBot;

public static class BotConfig {
    public static string Token => Required("DISCORD_TOKEN");

    public static string ConnectionString => Required("POSTGRES_CONNECTION_STRING");

    public static ulong? GuildId {
        get {
            var raw = Environment.GetEnvironmentVariable("DISCORD_GUILD_ID");
            if (string.IsNullOrWhiteSpace(raw)) {
                return null;
            }
            if (!ulong.TryParse(raw, out var id)) {
                throw new InvalidOperationException("DISCORD_GUILD_ID must be a numeric Discord guild id.");
            }
            return id;
        }
    }

    private static string Required(string name) {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) {
            throw new InvalidOperationException($"{name} is not set.");
        }
        return value;
    }
}
