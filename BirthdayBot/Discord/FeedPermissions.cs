using Discord.WebSocket;

namespace BirthdayBot.Discord;

public static class FeedPermissions {
    public static string? Missing(SocketGuild guild, SocketGuildChannel channel) {
        var permissions = guild.CurrentUser.GetPermissions(channel);
        if (!permissions.ViewChannel) return "View Channel";
        if (!permissions.SendMessages) return "Send Messages";
        return null;
    }

    public static bool CanMentionEveryone(SocketGuild guild, SocketGuildChannel channel) =>
        guild.CurrentUser.GetPermissions(channel).MentionEveryone;

    public static bool CanManage(SocketUser user) => user is SocketGuildUser { GuildPermissions.ManageGuild: true };
}
