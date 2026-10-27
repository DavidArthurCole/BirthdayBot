using System.Globalization;
using BirthdayBot.Discord;
using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;

namespace BirthdayBot.Commands;

public sealed record UpcomingEntry(ulong UserId, BirthDate Birthday, DateTimeOffset At, int? Age, bool IsToday);

public static class BirthdayCards {
    public const string EditId = "bday:edit";
    public const string RemoveId = "bday:remove";
    public const string CancelId = "bday:cancel";
    public const string FeedChannelId = "feed:channel";
    public const string FeedOffId = "feed:off";

    public static MessageComponent Profile(IUser user, bool own, BirthdayRow row, BirthDate birthday, DateTimeOffset now, bool feedConfigured) {
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
        if (own) {
            container.WithActionRow([
                Cards.Button("Change date", EditId, ButtonStyle.Secondary),
                Cards.Button("Remove", RemoveId, ButtonStyle.Danger)
            ], null);
        }
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

    public static MessageComponent FeedPanel(ulong? channelId, string? problem) {
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
        if (channelId is not null) {
            container.WithActionRow([Cards.Button("Stop announcing", FeedOffId, ButtonStyle.Danger)], null);
        }
        return new ComponentBuilderV2().WithContainer(container).Build();
    }
}
