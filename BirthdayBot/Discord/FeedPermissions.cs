using Discord.WebSocket;

namespace BirthdayBot.Discord;

public static class FeedPermissions {
    public static string? Missing(SocketGuild guild, SocketGuildChannel channel) {
        var permissions = guild.CurrentUser.GetPermissions(channel);
        if (!permissions.ViewChannel) return "View Channel";
        if (!permissions.SendMessages) return "Send Messages";
        return null;
    }
}
