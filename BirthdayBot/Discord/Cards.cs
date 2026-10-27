using System.Globalization;
using Discord;
using Discord.WebSocket;

namespace BirthdayBot.Discord;

public static class Cards {
    public static readonly Color Birthday = new(0xEB459E);
    public static readonly Color Success = new(0x57F287);
    public static readonly Color Danger = new(0xED4245);
    public static readonly Color Neutral = new(0x5865F2);

    public static MessageComponent Notice(Color accent, string title, string body, params IMessageComponentBuilder[] actions) {
        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .WithTextDisplay($"### {title}", null)
            .WithTextDisplay(body, null);
        if (actions.Length > 0) container.WithActionRow(actions, null);
        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    public static IMessageComponentBuilder Section(string text, string? imageUrl) {
        var display = new TextDisplayBuilder(text, null);
        if (imageUrl is null) return display;
        var thumbnail = new ThumbnailBuilder(new UnfurledMediaItemProperties(imageUrl), null, false, null);
        return new SectionBuilder(thumbnail, [display]);
    }

    public static ButtonBuilder Button(string label, string customId, ButtonStyle style) =>
        new ButtonBuilder().WithLabel(label).WithCustomId(customId).WithStyle(style);

    public static string Relative(DateTimeOffset at) =>
        string.Create(CultureInfo.InvariantCulture, $"<t:{at.ToUnixTimeSeconds()}:R>");

    public static Task ReplyAsync(IDiscordInteraction interaction, MessageComponent card) =>
        interaction.RespondAsync(components: card, ephemeral: true, allowedMentions: AllowedMentions.None, flags: MessageFlags.ComponentsV2);

    public static Task UpdateAsync(SocketMessageComponent interaction, MessageComponent card) =>
        interaction.UpdateAsync(message => {
            message.Components = card;
            message.AllowedMentions = AllowedMentions.None;
        });
}
