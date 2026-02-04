namespace OneWare.Essentials.PackageManager.Compatibility;

public enum CompatibilityRecordKind
{
    MissingDependency,
    RequiresCoreUpdate,
    PluginOutdated
}

public record CompatibilityRecord(
    string Dependency,
    Version? Required,
    Version? Provided,
    CompatibilityRecordKind Kind,
    string Message);

public class CompatibilityReport(
    bool isCompatible,
    string? report = null,
    IReadOnlyList<CompatibilityRecord>? records = null,
    IReadOnlyList<string>? suggestions = null)
{
    public bool IsCompatible { get; } = isCompatible;

    public string? Report { get; } = report;

    public IReadOnlyList<CompatibilityRecord> Records { get; } =
        records ?? Array.Empty<CompatibilityRecord>();

    public IReadOnlyList<string> Suggestions { get; } =
        suggestions ?? Array.Empty<string>();

    public bool StudioUpdateRequired { get; init; }
}
