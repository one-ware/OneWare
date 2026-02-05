namespace OneWare.Essentials.PackageManager;

public record PackageInstallContext(
    Package Package,
    PackageVersion Version,
    PackageTarget Target,
    string ExtractionPath,
    IProgress<float> Progress);
