namespace OneWare.Essentials.PackageManager;

/// <summary>A required plugin package. Minimum is inclusive; maximum is exclusive.</summary>
public sealed record PackageDependency
{
    public required string Id { get; init; }
    public string? MinVersion { get; init; }
    public string? MaxVersionExclusive { get; init; }

    public bool Accepts(string? version)
    {
        Validate();
        return SemanticVersion.TryParse(version, out var parsed)
               && (MinVersion == null || parsed >= Parse(MinVersion))
               && (MaxVersionExclusive == null || parsed < Parse(MaxVersionExclusive));
    }

    public void Validate()
    {
        // Use portable filename rules even on Unix: installed records may later be used on Windows.
        var stem = Id?.Split('.')[0];
        if (string.IsNullOrWhiteSpace(Id) || Id is "." or ".." || Id != Id.Trim() || Id.EndsWith('.') ||
            Id.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) ||
            InstalledPluginGraph.IsTransactionDirectoryName(Id) ||
            new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
            (stem is { Length: 4 } && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3])))
            throw new InvalidOperationException($"Invalid package ID '{Id}'.");
        if ((MinVersion != null && !SemanticVersion.TryParse(MinVersion, out _)) ||
            (MaxVersionExclusive != null && !SemanticVersion.TryParse(MaxVersionExclusive, out _)))
            throw new InvalidOperationException($"Invalid version bounds for {Id}.");
        if (MinVersion != null && MaxVersionExclusive != null &&
            Parse(MinVersion) >= Parse(MaxVersionExclusive))
            throw new InvalidOperationException($"Empty version range for {Id}.");
    }

    private static SemanticVersion Parse(string value)
    {
        SemanticVersion.TryParse(value, out var version);
        return version;
    }
}