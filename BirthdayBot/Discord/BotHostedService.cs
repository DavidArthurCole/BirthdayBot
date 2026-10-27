using BirthdayBot.Storage;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Discord;

public sealed class BotHostedService(
    DiscordSocketClient client,
    InteractionService interactions,
    InteractionRouter router,
    BirthdayStore birthdays,
    GuildSettingsStore settings,
    ILogger<BotHostedService> logger) : IHostedService {
    private bool _registered;

    public async Task StartAsync(CancellationToken cancellationToken) {
        client.Log += ForwardAsync;
        interactions.Log += ForwardAsync;
        client.Ready += OnReadyAsync;
        client.LeftGuild += OnLeftGuildAsync;
        await router.AddModulesAsync();
        await client.LoginAsync(TokenType.Bot, BotConfig.Token);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        await client.LogoutAsync();
        await client.StopAsync();
    }

    private Task ForwardAsync(LogMessage message) {
        var level = message.Severity switch {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            _ => LogLevel.Trace
        };
        logger.Log(level, message.Exception, "{Source}: {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    private async Task OnReadyAsync() {
        if (_registered) return;
        _registered = true;
        if (BotConfig.GuildId is { } guildId) {
            var guildCommands = await interactions.RegisterCommandsToGuildAsync(guildId);
            logger.LogInformation("Registered {Count} commands to guild {GuildId}", guildCommands.Count, guildId);
            return;
        }
        var globalCommands = await interactions.RegisterCommandsGloballyAsync();
        logger.LogInformation("Registered {Count} commands globally", globalCommands.Count);
    }

    private async Task OnLeftGuildAsync(SocketGuild guild) {
        await birthdays.DeleteGuildAsync(guild.Id, CancellationToken.None);
        await settings.DeleteGuildAsync(guild.Id, CancellationToken.None);
        logger.LogInformation("Left guild {GuildId} and deleted its rows", guild.Id);
    }
}
