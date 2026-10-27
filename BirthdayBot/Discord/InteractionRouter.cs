using BirthdayBot.Commands;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Discord;

public sealed class InteractionRouter(
    DiscordSocketClient client,
    InteractionService interactions,
    BirthdayForm form,
    BirthdayMessageForm messageForm,
    IServiceProvider services,
    ILogger<InteractionRouter> logger) {
    public async Task AddModulesAsync() {
        await interactions.AddModulesAsync(typeof(InteractionRouter).Assembly, services);
        client.InteractionCreated += OnInteractionCreatedAsync;
        interactions.InteractionExecuted += OnInteractionExecutedAsync;
    }

    private async Task OnInteractionCreatedAsync(SocketInteraction interaction) {
        if (interaction is SocketModal { Data.CustomId: BirthdayForm.ModalId } modal) {
            await form.HandleAsync(modal);
            return;
        }
        if (interaction is SocketModal messageModal && messageModal.Data.CustomId.StartsWith(BirthdayMessageForm.ModalPrefix, StringComparison.Ordinal)) {
            await messageForm.HandleAsync(messageModal);
            return;
        }
        var context = new SocketInteractionContext(client, interaction);
        await interactions.ExecuteCommandAsync(context, services);
    }

    private async Task OnInteractionExecutedAsync(ICommandInfo command, IInteractionContext context, IResult result) {
        if (result.IsSuccess || context.Interaction is IAutocompleteInteraction) return;
        logger.LogWarning("Interaction {Command} failed with {Error}: {Reason}", command?.Name, result.Error, result.ErrorReason);
        var body = result.Error switch {
            InteractionCommandError.UnknownCommand => "That button has expired. Run the command again.",
            InteractionCommandError.UnmetPrecondition => result.ErrorReason,
            _ => "That didn't work. Try again in a moment."
        };
        var card = Cards.Notice(Cards.Danger, "Something went wrong", body);
        if (context.Interaction.HasResponded) {
            await context.Interaction.FollowupAsync(components: card, ephemeral: true, allowedMentions: AllowedMentions.None, flags: MessageFlags.ComponentsV2);
            return;
        }
        await Cards.ReplyAsync(context.Interaction, card);
    }
}
