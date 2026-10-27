using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace BirthdayBot.Commands;

public sealed class BirthdayMessageForm(
    BirthdayMessageStore messages,
    TimeProvider time,
    ILogger<BirthdayMessageForm> logger) {
    public const string ModalPrefix = "bdaymsg:form:";
    public const int MaxLength = 1000;
    private const string MessageId = "message";

    public static Modal Build(IUser member, string? current) {
        var input = new TextInputBuilder()
            .WithCustomId(MessageId)
            .WithStyle(TextInputStyle.Paragraph)
            .WithPlaceholder("Happy birthday! Thanks for keeping this server weird.")
            .WithMaxLength(MaxLength)
            .WithRequired(false);
        if (current is not null) input.WithValue(current);
        return new ModalBuilder()
            .WithTitle("Birthday message")
            .WithCustomId(string.Create(CultureInfo.InvariantCulture, $"{ModalPrefix}{member.Id}"))
            .AddTextDisplay($"Posted with {member.Mention}'s birthday ping. They won't see it until the day.", null)
            .AddLabel(new LabelBuilder("Message", input, "Markdown and emoji work. Leave it empty to use the default.", null))
            .Build();
    }

    public async Task HandleAsync(SocketModal modal) {
        if (modal.GuildId is not { } guildId) return;
        if (!FeedPermissions.CanManage(modal.User)) {
            await Cards.ReplyAsync(modal, BirthdayCards.AdminsOnly());
            return;
        }
        if (!ulong.TryParse(modal.Data.CustomId.AsSpan(ModalPrefix.Length), CultureInfo.InvariantCulture, out var memberId)) return;
        var text = modal.Data.Components.FirstOrDefault(c => c.CustomId == MessageId)?.Value?.Trim() ?? "";
        if (text.Length == 0) {
            await messages.ClearAsync(guildId, memberId, CancellationToken.None);
            logger.LogInformation("User {AdminId} cleared birthday message for {UserId} in guild {GuildId}", modal.User.Id, memberId, guildId);
            await Cards.ReplyAsync(modal, Cards.Notice(Cards.Neutral, "Birthday message cleared", $"<@{memberId}> gets the default birthday post."));
            return;
        }
        await messages.SetAsync(guildId, memberId, text, modal.User.Id, time.GetUtcNow(), CancellationToken.None);
        logger.LogInformation("User {AdminId} set birthday message for {UserId} in guild {GuildId}", modal.User.Id, memberId, guildId);
        await Cards.ReplyAsync(modal, BirthdayCards.MessageSaved(memberId, text));
    }
}
