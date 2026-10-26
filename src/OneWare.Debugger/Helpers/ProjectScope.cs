namespace OneWare.Debugger.Helpers;

// Decides whether a file belongs to a project: the debugger only works on the active project.
public static class ProjectScope
{
    public static bool Contains(string? projectRoot, string file)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(file)) return false;

        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
            var path = Path.GetFullPath(file);

            // With the separator: "/a/proj" must not swallow "/a/proj2/main.c".
            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Invalid path characters -> not part of any project.
            return false;
        }
    }
}
