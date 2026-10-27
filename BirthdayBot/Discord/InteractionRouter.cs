using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Discord;

public sealed class InteractionRouter(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    ILogger<InteractionRouter> logger) {
    public async Task AddModulesAsync() {
        await interactions.AddModulesAsync(typeof(InteractionRouter).Assembly, services);
        client.InteractionCreated += OnInteractionCreatedAsync;
        interactions.InteractionExecuted += OnInteractionExecutedAsync;
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction) {
        var context = new SocketInteractionContext(client, interaction);
        await interactions.ExecuteCommandAsync(context, services);
    }

    private async Task OnInteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result) {
        if (result.IsSuccess) return;
        logger.LogWarning("Interaction {Command} failed with {Error}: {Reason}", command?.Name, result.Error, result.ErrorReason);
        if (context.Interaction.HasResponded) {
            await context.Interaction.FollowupAsync(result.ErrorReason, ephemeral: true);
            return;
        }
        await context.Interaction.RespondAsync(result.ErrorReason, ephemeral: true);
    }
}
