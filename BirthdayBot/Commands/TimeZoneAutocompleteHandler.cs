using BirthdayBot.Time;
using Discord;
using Discord.Interactions;

namespace BirthdayBot.Commands;

public sealed class TimeZoneAutocompleteHandler : AutocompleteHandler {
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction, IParameterInfo parameter, IServiceProvider services) {
        var current = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";
        var results = TimeZoneCatalog.Search(current).Select(id => new AutocompleteResult(id, id));
        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
