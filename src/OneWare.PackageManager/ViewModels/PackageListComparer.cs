namespace OneWare.PackageManager.ViewModels;

/// <summary>
///     Orders packages by a key that never depends on package status, so installing or updating a
///     package can never move its row. While searching, a relevance tier is applied first, which also
///     only depends on the query.
/// </summary>
public sealed class PackageListComparer(string filter) : IComparer<PackageViewModel>
{
    public int Compare(PackageViewModel? x, PackageViewModel? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        var relevance = GetRelevance(x.PackageState.Package.Name, filter)
            .CompareTo(GetRelevance(y.PackageState.Package.Name, filter));
        if (relevance != 0) return relevance;

        var name = string.Compare(x.PackageState.Package.Name, y.PackageState.Package.Name,
            StringComparison.OrdinalIgnoreCase);
        if (name != 0) return name;

        // Only the id is guaranteed unique, it keeps the order deterministic for duplicate names.
        return string.Compare(x.PackageState.Package.Id, y.PackageState.Package.Id,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Lower is more relevant: exact match, then prefix match, then substring match.
    /// </summary>
    public static int GetRelevance(string? name, string filter)
    {
        if (string.IsNullOrEmpty(filter)) return 0;
        if (string.IsNullOrEmpty(name)) return 3;

        if (name.Equals(filter, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) return 1;
        return name.Contains(filter, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    }
}
