using BirthdayBot.Time;

namespace BirthdayBot.Tests;

public sealed class TimeZoneCatalogTests {
    [Fact]
    public void Every_id_resolves() {
        Assert.NotEmpty(TimeZoneCatalog.Ids);
        Assert.All(TimeZoneCatalog.Ids, id => Assert.NotNull(TimeZoneCatalog.Resolve(id)));
    }

    [Fact]
    public void Ids_are_iana() {
        Assert.All(TimeZoneCatalog.Ids, id => Assert.Contains('/', id));
        Assert.Equal(TimeZoneCatalog.Ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), TimeZoneCatalog.Ids);
    }

    [Fact]
    public void Search_is_case_insensitive() {
        var id = TimeZoneCatalog.Ids[0];
        var upper = TimeZoneCatalog.Search(id.ToUpperInvariant());
        var lower = TimeZoneCatalog.Search(id.ToLowerInvariant());
        Assert.Equal(upper, lower);
        Assert.Contains(id, upper);
    }

    [Fact]
    public void Search_prefers_prefix_matches() {
        var results = TimeZoneCatalog.Search("Pa", 1000);
        var prefixCount = results.TakeWhile(id => id.StartsWith("Pa", StringComparison.OrdinalIgnoreCase)).Count();
        Assert.NotEqual(0, prefixCount);
        Assert.NotEqual(results.Count, prefixCount);
        Assert.All(results.Skip(prefixCount), id => {
            Assert.False(id.StartsWith("Pa", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Pa", id, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Search_caps_at_limit() {
        Assert.Equal(5, TimeZoneCatalog.Search("", 5).Count);
        Assert.Equal(TimeZoneCatalog.Ids.Take(25), TimeZoneCatalog.Search(""));
    }

    [Fact]
    public void Resolve_unknown_returns_null() {
        Assert.Null(TimeZoneCatalog.Resolve("Nowhere/Nothing"));
    }
}
