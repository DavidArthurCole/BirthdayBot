using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;

namespace BirthdayBot.Commands;

public sealed record UpcomingEntry(ulong UserId, BirthDate Birthday, DateTimeOffset At, int? Age, bool IsToday);

public sealed record ProfileAdmin(string? Message);

public static class BirthdayCards {
    public const string EditId = "bday:edit";
    public const string RemoveId = "bday:remove";
    public const string CancelId = "bday:cancel";
    public const string FeedChannelId = "feed:channel";
    public const string FeedOffId = "feed:off";
    public const string FeedEveryoneId = "feed:everyone";
    public const string MessageMemberId = "bdaymsg:member";
    public const string MessageOpenPrefix = "bdaymsg:open:";

    public static MessageComponent AdminsOnly() =>
        Cards.Notice(Cards.Danger, "Admins only", "You need **Manage Server** to do that.");

    public static MessageComponent MessageSaved(ulong memberId, string message) {
        var container = new ContainerBuilder()
            .WithAccentColor(Cards.Success)
            .WithTextDisplay("### Birthday message saved", null)
            .WithTextDisplay($"Posted with <@{memberId}>'s next birthday ping:", null)
            .WithTextDisplay(Quote(message), null)
            .WithActionRow([Cards.Button("Edit", MessageOpenPrefix + memberId.ToString(CultureInfo.InvariantCulture), ButtonStyle.Secondary)], null);
        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    public static string Quote(string message) => string.Join("\n", message.Split('\n').Select(line => $"> {line}"));

    public static MessageComponent Profile(IUser user, bool own, BirthdayRow row, BirthDate birthday, DateTimeOffset now, bool feedConfigured, ProfileAdmin? admin = null) {
        var zone = TimeZoneCatalog.Resolve(row.TimeZone) ?? TimeZoneInfo.Utc;
        var today = LocalClock.Today(now, zone);
        var next = birthday.NextOccurrence(today);
        var age = birthday.AgeOn(next);
        var isToday = next == today;
        var when = (isToday, age) switch {
            (true, { } a) => $"Turned **{a}** today. Happy birthday!",
            (true, null) => "It's today. Happy birthday!",
            (false, { } a) => $"Turns **{a}** {Cards.Relative(LocalClock.MidnightUtc(next, zone))}",
            _ => $"Next birthday {Cards.Relative(LocalClock.MidnightUtc(next, zone))}"
        };
        var title = own ? "## Your birthday" : $"## {user.Mention}'s birthday";
        var text = $"{title}\n**{birthday.Format()}**\n{when}\n-# {row.TimeZone.Replace('_', ' ')}, {LocalClock.Offset(now, zone)}";
        var container = new ContainerBuilder()
            .WithAccentColor(Cards.Birthday);
        container.AddComponent(Cards.Section(text, user.GetDisplayAvatarUrl(ImageFormat.Auto, 256)));
        if (own && !feedConfigured) {
            container.WithSeparator(SeparatorSpacingSize.Small, true, null);
            container.WithTextDisplay("-# This server has no birthday channel yet, so nothing gets announced. An admin can pick one with `/birthday-feed`.", null);
        }
        if (admin is not null && !own) {
            container.WithSeparator(SeparatorSpacingSize.Small, true, null);
            container.WithTextDisplay(admin.Message is { } message
                ? $"-# Birthday message, only admins see this\n{Quote(message)}"
                : "-# No custom birthday message. The default post is used.", null);
        }
        List<IMessageComponentBuilder> buttons = [];
        if (own) {
            buttons.Add(Cards.Button("Change date", EditId, ButtonStyle.Secondary));
            buttons.Add(Cards.Button("Remove", RemoveId, ButtonStyle.Danger));
        }
        if (admin is not null && !own) {
            var label = admin.Message is null ? "Write birthday message" : "Edit birthday message";
            buttons.Add(Cards.Button(label, MessageOpenPrefix + user.Id.ToString(CultureInfo.InvariantCulture), ButtonStyle.Primary));
        }
        if (buttons.Count > 0) container.WithActionRow(buttons, null);
        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    public static MessageComponent NotSaved(IUser user, bool own) => own
        ? Cards.Notice(Cards.Neutral, "No birthday saved", "Run `/birthday set` to save yours. The year is optional.")
        : Cards.Notice(Cards.Neutral, "No birthday saved", $"{user.Mention} hasn't saved a birthday in this server.");

    public static MessageComponent Upcoming(IReadOnlyList<UpcomingEntry> entries, int total) {
        if (entries.Count == 0) {
            return Cards.Notice(Cards.Neutral, "Upcoming birthdays", "Nobody here has saved a birthday yet. Be the first with `/birthday set`.");
        }
        var lines = entries.Select(entry => {
            var date = (entry.Birthday with { Year = null }).Format();
            var when = entry.IsToday ? "**today**" : Cards.Relative(entry.At);
            if (entry.Age is not { } a) return $"<@{entry.UserId}>  **{date}**  {when}";
            var verb = entry.IsToday ? "turned" : "turning";
            return string.Create(CultureInfo.InvariantCulture, $"<@{entry.UserId}>  **{date}**  {when}, {verb} {a}");
        });
        var footer = total == 1 ? "-# 1 birthday saved in this server" : string.Create(CultureInfo.InvariantCulture, $"-# {total} birthdays saved in this server");
        var container = new ContainerBuilder()
            .WithAccentColor(Cards.Birthday)
            .WithTextDisplay("## Upcoming birthdays", null)
            .WithTextDisplay(string.Join("\n", lines), null)
            .WithSeparator(SeparatorSpacingSize.Small, true, null)
            .WithTextDisplay(footer, null);
        return new ComponentBuilderV2().WithContainer(container).Build();
    }

    public static MessageComponent ConfirmRemove() => Cards.Notice(
        Cards.Danger,
        "Remove your birthday?",
        "You won't be pinged in this server any more. Your time zone is removed too.",
        Cards.Button("Remove", RemoveId, ButtonStyle.Danger),
        Cards.Button("Keep it", CancelId, ButtonStyle.Secondary));

    public static MessageComponent Removed() =>
        Cards.Notice(Cards.Neutral, "Birthday removed", "You won't be pinged in this server. Run `/birthday set` any time to add it back.");

    public static MessageComponent FeedPanel(GuildSettings settings, string? problem) {
        var channelId = settings.FeedChannelId;
        var body = channelId is { } id
            ? $"Birthday pings post in <#{id}> at midnight in each member's own time zone."
            : "Birthdays are saved but not announced. Pick a channel below to start posting birthday pings.";
        var accent = (problem, channelId) switch {
            (not null, _) => Cards.Danger,
            (null, null) => Cards.Neutral,
            _ => Cards.Success
        };
        var select = new SelectMenuBuilder()
            .WithCustomId(FeedChannelId)
            .WithType(ComponentType.ChannelSelect)
            .WithChannelTypes(ChannelType.Text, ChannelType.News)
            .WithPlaceholder("Choose the birthday channel")
            .WithMinValues(1)
            .WithMaxValues(1);
        if (channelId is { } current) select.AddDefaultValue(current, SelectDefaultValueType.Channel);
        var container = new ContainerBuilder()
            .WithAccentColor(accent)
            .WithTextDisplay("## Birthday channel", null)
            .WithTextDisplay(body, null);
        if (problem is not null) container.WithTextDisplay($"**Heads up:** {problem}", null);
        container.WithActionRow([select], null);
        var everyone = settings.PingEveryone
            ? "**@everyone is on.** Each birthday post pings the whole server."
            : "**@everyone is off.** Only the birthday person is pinged.";
        var toggle = settings.PingEveryone
            ? Cards.Button("Turn off @everyone", FeedEveryoneId, ButtonStyle.Secondary)
            : Cards.Button("Turn on @everyone", FeedEveryoneId, ButtonStyle.Success);
        container.WithSeparator(SeparatorSpacingSize.Small, true, null);
        container.WithTextDisplay(everyone, null);
        List<IMessageComponentBuilder> buttons = [toggle];
        if (channelId is not null) buttons.Add(Cards.Button("Stop announcing", FeedOffId, ButtonStyle.Danger));
        container.WithActionRow(buttons, null);
        var member = new SelectMenuBuilder()
            .WithCustomId(MessageMemberId)
            .WithType(ComponentType.UserSelect)
            .WithPlaceholder("Write a birthday message for a member")
            .WithMinValues(1)
            .WithMaxValues(1);
        container.WithSeparator(SeparatorSpacingSize.Small, true, null);
        container.WithTextDisplay("**Birthday messages.** Pick a member to write something that posts with their birthday ping. You can also right-click a member and choose **Apps > Birthday message**.", null);
        container.WithActionRow([member], null);
        return new ComponentBuilderV2().WithContainer(container).Build();
    }
}
