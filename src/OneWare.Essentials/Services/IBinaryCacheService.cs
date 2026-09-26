namespace OneWare.Essentials.Services;

/// <summary>
///     Provides persistent, versioned copies of directories (e.g. plugins or native runtimes).
///     Binaries are loaded from the copy so the original installation can be updated or removed
///     while the application is running, without copying the files again on every start.
/// </summary>
public interface IBinaryCacheService
{
    /// <summary>
    ///     Returns a directory containing an up-to-date copy of <paramref name="sourceDirectory" />.
    ///     The copy is only created when the source changed since the last cached version and stays
    ///     valid for the lifetime of this process. Returns null if the copy could not be created.
    /// </summary>
    /// <param name="category">Cache category, e.g. "Plugins" or "OnnxRuntimes".</param>
    /// <param name="name">Unique name inside the category, usually the folder name of the source.</param>
    /// <param name="sourceDirectory">Directory to copy.</param>
    string? GetOrCreateCopy(string category, string name, string sourceDirectory);

    /// <summary>
    ///     Removes outdated cache entries that are no longer used by any running instance.
    /// </summary>
    void CleanupUnusedEntries();
}
