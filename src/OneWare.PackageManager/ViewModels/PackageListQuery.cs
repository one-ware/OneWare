namespace OneWare.PackageManager.ViewModels;

/// <summary>
///     Everything the package list layout depends on. Membership and order are a pure function of this
///     query, so they only change when the user changes it, never when package data changes.
/// </summary>
public sealed record PackageListQuery(string Filter, bool ShowInstalled, bool ShowAvailable)
{
    public static readonly PackageListQuery Default = new(string.Empty, true, true);

    public bool HasSearch => !string.IsNullOrEmpty(Filter);
}
