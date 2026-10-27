using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace BirthdayBot.Commands;

[CommandContextType(InteractionContextType.Guild)]
public sealed class BirthdayMessageModule(BirthdayMessageStore messages) : InteractionModuleBase<SocketInteractionContext> {
    [UserCommand("Birthday message")]
    [DefaultMemberPermissions(GuildPermission.ManageGuild)]
    public async Task FromUserAsync(IUser member) => await OpenAsync(member.Id);

    [ComponentInteraction(BirthdayCards.MessageMemberId, true)]
    public async Task FromSelectAsync() {
        var component = (SocketMessageComponent)Context.Interaction;
        if (!ulong.TryParse(component.Data.Values.FirstOrDefault(), CultureInfo.InvariantCulture, out var memberId)) return;
        await OpenAsync(memberId);
    }

    [ComponentInteraction(BirthdayCards.MessageOpenPrefix + "*", true)]
    public async Task FromButtonAsync(string memberId) {
        if (!ulong.TryParse(memberId, CultureInfo.InvariantCulture, out var id)) return;
        await OpenAsync(id);
    }

    private async Task OpenAsync(ulong memberId) {
        if (!FeedPermissions.CanManage(Context.User)) {
            await Cards.ReplyAsync(Context.Interaction, BirthdayCards.AdminsOnly());
            return;
        }
        IUser? member = Context.Guild.GetUser(memberId) ?? await Context.Client.GetUserAsync(memberId);
        if (member is null || member.IsBot) {
            await Cards.ReplyAsync(Context.Interaction, Cards.Notice(Cards.Danger, "Can't do that", "Birthday messages are for members, not bots."));
            return;
        }
        var current = await messages.GetAsync(Context.Guild.Id, memberId, CancellationToken.None);
        await RespondWithModalAsync(BirthdayMessageForm.Build(member, current));
    }
}
