namespace OneWare.Essentials.PackageManager;

/// <summary>Offline startup order. Invalid components are blocked without suppressing unrelated plugins.</summary>
public static class InstalledPluginGraph
{
    public sealed record LoadOrder(IReadOnlyList<InstalledPackage> Packages, IReadOnlyDictionary<string, string> Blocked);

    public static bool IsTransactionDirectoryName(string name) =>
        name.Contains(".stage-", StringComparison.OrdinalIgnoreCase) ||
        name.Contains(".backup-", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldDiscoverLegacyDirectory(string name, IEnumerable<string> managedIds) =>
        !IsTransactionDirectoryName(name) && !managedIds.Contains(name, StringComparer.OrdinalIgnoreCase);

    public static LoadOrder Resolve(IEnumerable<InstalledPackage> records, Func<string, bool> directoryExists)
    {
        var blocked = new Dictionary<string, string>(StringComparer.Ordinal);
        var groups = records.Where(x => x.Type == "Plugin").GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var group in groups.Where(x => x.Count() > 1))
            foreach (var record in group) blocked[record.Id] = $"Ambiguous installed plugin ID: {record.Id}.";
        var packages = groups.Where(x => x.Count() == 1).Select(x => x.Single())
            .ToDictionary(x => x.Id, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<InstalledPackage>();
        bool Visit(string id)
        {
            if (blocked.ContainsKey(id)) return false;
            if (done.Contains(id)) return true;
            if (!visiting.Add(id)) { blocked[id] = $"Dependency cycle at {id}."; return false; }
            var record = packages[id];
            try
            {
                new PackageDependency { Id = id }.Validate();
                if (!directoryExists(id)) throw new InvalidOperationException($"Plugin directory missing: {id}.");
                foreach (var dependency in record.Dependencies ?? [])
                {
                    dependency.Validate();
                    if (!packages.TryGetValue(dependency.Id, out var provider) || !dependency.Accepts(provider.InstalledVersion))
                        throw new InvalidOperationException($"{id} requires {dependency.Id} [{dependency.MinVersion ?? "*"}, {dependency.MaxVersionExclusive ?? "*"}). Repair the installation.");
                    if (!Visit(dependency.Id)) throw new InvalidOperationException($"{id} blocked by {dependency.Id}: {blocked.GetValueOrDefault(dependency.Id)}");
                }
                done.Add(id);
                order.Add(record);
                return true;
            }
            catch (InvalidOperationException ex) { blocked[id] = ex.Message; return false; }
            finally { visiting.Remove(id); }
        }
        foreach (var id in packages.Keys.OrderBy(x => x, StringComparer.Ordinal)) Visit(id);
        return new(order.AsReadOnly(), blocked);
    }

    public static IReadOnlyList<string> DependencyClosure(string id, IReadOnlyDictionary<string, InstalledPackage> records)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string current)
        {
            foreach (var dep in records[current].Dependencies ?? [])
                if (result.Add(dep.Id)) Visit(dep.Id);
        }
        Visit(id);
        return result.ToArray();
    }
}