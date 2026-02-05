namespace OneWare.Essentials.PackageManager.Compatibility;

public enum CompatibilityRecordKind
{
    MissingDependency,
    RequiresCoreUpdate,
    PluginOutdated
}

public record CompatibilityIssue(
    string Dependency,
    Version? Required,
    Version? Provided,
    CompatibilityRecordKind Kind);

public class CompatibilityReport(
    bool isCompatible,
    IReadOnlyList<CompatibilityIssue>? issues = null)
{
    public bool IsCompatible { get; } = isCompatible;
    
    // Compatibility report in Markdown
    public string Report
    {
        get
        {
            if (IsCompatible) 
                return " ✓ Compatible";

            var report = "⚠ ";

            if (StudioUpdateSuggested)
                report += "This package requires a newer Version of **OneWare Studio**! \n\n > Please update **OneWare Studio** to the latest version.\n\n";
            else
                report += "This package version appears to be outdated and is **not compatible**. \n\n";

            report += "| Dependency | Required | Provided |\n";
            report += "|------------|----------|----------|\n";

            foreach (var issue in Issues)
            {
                report += $"| {issue.Dependency} | **{issue.Required}** | **{issue.Provided}** |\n";
            }

            return report;
        }
    }

    public IReadOnlyList<CompatibilityIssue> Issues { get; } = issues ?? [];
    
    public bool StudioUpdateSuggested => Issues.Any(r => r.Kind == CompatibilityRecordKind.RequiresCoreUpdate);
}
