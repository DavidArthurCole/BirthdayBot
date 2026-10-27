namespace BirthdayBot.Time;

public static class TimeZoneCatalog {
    public static IReadOnlyList<string> Ids { get; } = LoadIds();

    public static IReadOnlyList<string> Search(string query, int limit = 25) {
        var trimmed = query.Trim();
        if (trimmed.Length == 0) {
            return [.. Ids.Take(limit)];
        }
        var prefix = Ids.Where(id => id.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));
        var contains = Ids.Where(id => !id.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase) && id.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        return [.. prefix.Concat(contains).Take(limit)];
    }

    public static TimeZoneInfo? Resolve(string id) {
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        } catch (Exception) {
            return null;
        }
    }

    private static List<string> LoadIds() {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones()) {
            if (zone.Id.Contains('/')) {
                ids.Add(zone.Id);
            } else if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana)) {
                ids.Add(iana);
            }
        }
        return [.. ids];
    }
}
