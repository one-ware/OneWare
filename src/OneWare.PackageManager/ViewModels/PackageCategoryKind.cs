namespace OneWare.PackageManager.ViewModels;

public enum PackageCategoryKind
{
    Normal,

    /// <summary>
    ///     The single tree root aggregating every package. Never matched by the search, never hidden.
    /// </summary>
    Root,

    /// <summary>
    ///     Smart category listing the packages that have an update available.
    /// </summary>
    Updates
}
