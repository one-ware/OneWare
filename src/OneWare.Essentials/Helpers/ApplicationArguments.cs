namespace OneWare.Essentials.Helpers;

/// <summary>
/// Holds command line information provided by the application entry point.
/// </summary>
public static class ApplicationArguments
{
    /// <summary>
    /// Arguments that should be passed to a new process when the application restarts itself.
    /// This is set by the entry point, which knows the command line schema, and usually contains
    /// all options of the current process without the positional launch argument (file/folder/URI),
    /// which must not be opened again after a restart.
    /// </summary>
    public static IReadOnlyList<string>? RestartArguments { get; set; }
}
