using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.Services;

namespace OneWare.PackageManager.Services;

/// <summary>Deterministic, side-effect-free backtracking over an exact catalog/installed snapshot.</summary>
public sealed class PackageDependencyResolver
{
    public PackageOperationPlan Resolve(IReadOnlyDictionary<string, Package> catalog,
        IReadOnlyDictionary<string, InstalledPackage> installed, IReadOnlyList<PackageRequest> roots,
        Func<Package, PackageVersion, bool> supportsTarget, CancellationToken cancellationToken = default)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Catalog = catalog.OrderBy(x => x.Key, StringComparer.Ordinal),
                Installed = installed.OrderBy(x => x.Key, StringComparer.Ordinal), Roots = roots }))));
        var error = "No compatible dependency solution exists.";
        try
        {
            if (roots.Count == 0) throw new InvalidOperationException("Select at least one package.");
            var requests = roots.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var ids = catalog.Keys.Concat(installed.Keys).Distinct(StringComparer.Ordinal).ToArray();
            if (ids.Distinct(StringComparer.OrdinalIgnoreCase).Count() != ids.Length)
                throw new InvalidOperationException("Ambiguous package IDs differ only in letter case.");
            foreach (var id in ids) new PackageDependency { Id = id }.Validate();
            foreach (var (id, package) in catalog)
            {
                if (id != package.Id) throw new InvalidOperationException($"Package ID does not match catalog key '{id}'.");
                if (package.Versions?.GroupBy(x => x.Version, StringComparer.Ordinal).Any(x => x.Count() > 1) == true)
                    throw new InvalidOperationException($"{id}: duplicate package versions are not allowed.");
            }
            foreach (var (id, record) in installed)
                if (id != record.Id) throw new InvalidOperationException($"Installed package ID does not match key '{id}'.");

            Dictionary<string, PackageVersion>? Search(Dictionary<string, PackageVersion> selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var constraints = new Dictionary<string, List<(PackageDependency Dependency, string Parent)>>(StringComparer.Ordinal);
                var needed = new HashSet<string>(requests.Keys, StringComparer.Ordinal);
                void Add(PackageDependency dependency, string parent, bool required)
                {
                    dependency.Validate();
                    if (!constraints.TryGetValue(dependency.Id, out var list)) constraints[dependency.Id] = list = [];
                    list.Add((dependency, parent));
                    if (required) needed.Add(dependency.Id);
                }
                foreach (var (id, version) in selected)
                {
                    if (version.Dependencies is { Length: > 0 } && catalog[id].Type != "Plugin")
                        throw new InvalidOperationException($"{id}: only plugins may declare dependencies.");
                    if (version.Dependencies?.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1) == true)
                        throw new InvalidOperationException($"{id}: duplicate dependency IDs are not allowed.");
                    foreach (var dep in version.Dependencies ?? [])
                    {
                        if (!catalog.TryGetValue(dep.Id, out var dependencyPackage) || dependencyPackage.Type != "Plugin")
                        {
                            error = $"{id} -> {dep.Id}: required plugin is missing or is not a plugin.";
                            return null;
                        }
                        Add(dep, id, true);
                    }
                }
                // All installed reverse dependents constrain standalone upgrades, including offline records.
                foreach (var record in installed.Values.Where(x => !requests.ContainsKey(x.Id) && !selected.ContainsKey(x.Id)))
                    foreach (var dep in record.Dependencies ??
                             catalog.GetValueOrDefault(record.Id)?.Versions?.FirstOrDefault(v => v.Version == record.InstalledVersion)?.Dependencies ?? [])
                        Add(dep, record.Id + " (installed)", false);

                foreach (var (id, version) in selected)
                    if (constraints.TryGetValue(id, out var bounds) && bounds.Any(x => !x.Dependency.Accepts(version.Version)))
                    {
                        error = $"Version conflict for {id} {version.Version}: " + string.Join(", ", bounds.Select(x =>
                            $"{x.Parent} -> {id} [{x.Dependency.MinVersion ?? "*"}, {x.Dependency.MaxVersionExclusive ?? "*"})"));
                        return null;
                    }

                var pending = needed.Where(x => !selected.ContainsKey(x)).OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault();
                if (pending == null)
                {
                    try { Order(selected); return selected; }
                    catch (InvalidOperationException ex) { error = ex.Message; return null; }
                }
                if (!catalog.TryGetValue(pending, out var package))
                {
                    error = $"Required package '{pending}' is missing.";
                    return null;
                }
                var request = requests.GetValueOrDefault(pending);
                var current = installed.GetValueOrDefault(pending);
                var candidates = (package.Versions ?? []).Where(v => SemanticVersion.TryParse(v.Version, out _))
                    .Where(v => request?.Version != null ? v.Version == request.Version :
                        (!(v.IsPrerelease || Parse(v.Version).IsPrerelease) || request?.IncludePrerelease == true || v.Version == current?.InstalledVersion))
                    .Where(v => v.IsSupportedByStudio() && (v.Version == current?.InstalledVersion || supportsTarget(package, v)))
                    .Where(v => current == null || Compare(v.Version, current.InstalledVersion) >= 0)
                    .OrderByDescending(v => request == null && v.Version == current?.InstalledVersion)
                    .ThenByDescending(v => Parse(v.Version)).ToArray();
                if (candidates.Length == 0) error = $"{pending}: no supported version/target satisfies the request (downgrades are not automatic).";
                foreach (var candidate in candidates)
                {
                    var version = candidate;
                    if (current != null && candidate.Version == current.InstalledVersion && current.Dependencies != null)
                        version = new PackageVersion { Version = candidate.Version, Dependencies = current.Dependencies,
                            Targets = candidate.Targets, IsPrerelease = candidate.IsPrerelease, MinStudioVersion = candidate.MinStudioVersion,
                            CompatibilityUrl = candidate.CompatibilityUrl };
                    var next = new Dictionary<string, PackageVersion>(selected, StringComparer.Ordinal) { [pending] = version };
                    if (Search(next) is { } solved) return solved;
                }
                return null;
            }

            var solution = Search(new Dictionary<string, PackageVersion>(StringComparer.Ordinal));
            if (solution == null) return new(fingerprint, roots.ToArray(), [], [error]);
            var items = Order(solution).Select(id =>
            {
                var version = solution[id];
                var previous = installed.GetValueOrDefault(id)?.InstalledVersion;
                var action = previous == version.Version ? PackagePlanAction.Reuse : previous == null ? PackagePlanAction.Install : PackagePlanAction.Update;
                return new PackagePlanItem(id, catalog[id].Name ?? id, version.Version!, previous, action,
                    action != PackagePlanAction.Reuse && catalog[id].AcceptLicenseBeforeDownload,
                    Array.AsReadOnly(version.Dependencies ?? []));
            }).ToArray();
            // The same metadata can resolve differently when platform/installer capabilities change.
            fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new { Snapshot = fingerprint, Items = items }))));
            return new(fingerprint, Array.AsReadOnly(roots.ToArray()), Array.AsReadOnly(items), []);
        }
        catch (InvalidOperationException ex) { return new(fingerprint, roots.ToArray(), [], [ex.Message]); }
        catch (ArgumentException ex) { return new(fingerprint, roots.ToArray(), [], [ex.Message]); }
    }

    private static List<string> Order(Dictionary<string, PackageVersion> versions)
    {
        var ordered = new List<string>();
        var visiting = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (done.Contains(id)) return;
            if (visiting.Contains(id)) throw new InvalidOperationException("Dependency cycle: " + string.Join(" -> ", visiting.Append(id)));
            visiting.Add(id);
            foreach (var dependency in versions[id].Dependencies ?? []) Visit(dependency.Id);
            visiting.RemoveAt(visiting.Count - 1);
            done.Add(id);
            ordered.Add(id);
        }
        foreach (var id in versions.Keys.OrderBy(x => x, StringComparer.Ordinal)) Visit(id);
        return ordered;
    }

    private static SemanticVersion Parse(string? version) { SemanticVersion.TryParse(version, out var result); return result; }
    private static int Compare(string? left, string? right) => Parse(left).CompareTo(Parse(right));
}