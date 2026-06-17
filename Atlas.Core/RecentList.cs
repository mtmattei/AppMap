namespace Atlas.Core;

/// <summary>
/// Pure most-recently-used list maintenance for the recent-models store: promote a path to the
/// front, drop any prior duplicate, cap the length. File IO lives in the app's store impl.
/// </summary>
public static class RecentList
{
    public const int DefaultCapacity = 8;

    /// <summary>
    /// Returns a new list with <paramref name="path"/> at the front, any prior case-insensitive
    /// duplicate removed (Windows paths are case-insensitive), capped at <paramref name="capacity"/>.
    /// </summary>
    public static IReadOnlyList<string> Promote(IReadOnlyList<string> current, string path, int capacity = DefaultCapacity)
    {
        var result = new List<string>(current.Count + 1) { path };
        result.AddRange(current.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)));
        return result.Count > capacity ? result.GetRange(0, capacity) : result;
    }
}
