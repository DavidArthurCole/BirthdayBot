using System.Globalization;
using BirthdayBot.Storage;
using BirthdayBot.Time;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace BirthdayBot.Commands;

public sealed class TimeZoneAutocompleteHandler : AutocompleteHandler {
    private const int Limit = 25;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var query = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var ids = TimeZoneCatalog.Search(query, Limit).ToList();
        string? saved = null;
        if (query.Trim().Length == 0 && context.Guild is { } guild) {
            var row = await services.GetRequiredService<BirthdayStore>().GetAsync(guild.Id, context.User.Id, CancellationToken.None);
            if (row is not null && TimeZoneCatalog.Resolve(row.TimeZone) is not null) {
                saved = row.TimeZone;
                ids.Remove(saved);
                ids.Insert(0, saved);
            }
        }
        var results = ids
            .Take(Limit)
            .Select(id => TimeZoneCatalog.Resolve(id) is { } zone ? new AutocompleteResult(Label(id, zone, now, id == saved), id) : null)
            .OfType<AutocompleteResult>();
        return AutocompletionResult.FromSuccess(results);
    }

    private static string Label(string id, TimeZoneInfo zone, DateTimeOffset now, bool saved) {
        var local = TimeZoneInfo.ConvertTime(now, zone).ToString("h:mm tt", CultureInfo.InvariantCulture);
        var suffix = saved ? ", saved" : "";
        return $"{id.Replace('_', ' ')} ({LocalClock.Offset(now, zone)}, now {local}{suffix})";
    }
}
