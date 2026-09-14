using OneWare.Essentials.PackageManager;

namespace OneWare.PackageManager.Services;

public static class PackageSearch
{
    /// <returns>Lower scores rank first; null excludes the package.</returns>
    public static int? Score(Package package, string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var name = package.Name ?? "";
        var id = package.Id ?? "";
        var text = $"{name} {id} {package.Description} {package.Category} {package.Type}";
        if (tokens.Any(t => !text.Contains(t, StringComparison.OrdinalIgnoreCase))) return null;
        if (name.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase) || id.Equals(query.Trim(), StringComparison.OrdinalIgnoreCase)) return 0;
        return tokens.All(t => name.Contains(t, StringComparison.OrdinalIgnoreCase) || id.Contains(t, StringComparison.OrdinalIgnoreCase)) ? 1 : 2;
    }
}