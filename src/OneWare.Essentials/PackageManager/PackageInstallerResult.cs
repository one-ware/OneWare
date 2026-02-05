using OneWare.Essentials.Enums;

namespace OneWare.Essentials.PackageManager;

public record PackageInstallerResult(PackageStatus Status, string? InstalledVersionWarningText = null);
