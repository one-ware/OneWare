using OneWare.Essentials.Enums;

namespace OneWare.Essentials.PackageManager;

public class PackageProgressEventArgs(
    string packageId,
    PackageStatus status,
    float progress,
    bool isIndeterminate) : EventArgs
{
    public string PackageId { get; } = packageId;

    public PackageStatus Status { get; } = status;

    public float Progress { get; } = progress;

    public bool IsIndeterminate { get; } = isIndeterminate;
}
