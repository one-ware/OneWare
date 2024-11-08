namespace OneWare.Essentials.PackageManager.Compatibility;

public class CompatibilityReport(bool isCompatible, string? report = null)
{
    public bool IsCompatible { get; } = isCompatible;

    public string? Report { get; } = report;
}