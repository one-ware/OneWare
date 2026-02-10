namespace OneWare.Essentials.Extensions;

public static class StringExtensions
{
    public static string RemoveWhitespace(this string input)
    {
        return new string(input.ToCharArray()
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());
    }

    public static bool EqualPaths(this string input, string? otherPath)
    {
        if (otherPath == null) return false;
        if (input == otherPath) return true;

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(input.NormalizePath(), otherPath.NormalizePath(), comparison);
    }

    public static bool ContainsSubPath(this string pathToFile, string subPath)
    {
        pathToFile = Path.GetDirectoryName(pathToFile) + "\\";
        var searchPath = Path.GetDirectoryName(subPath) + "\\";

        var containsIt = pathToFile.IndexOf(searchPath, StringComparison.OrdinalIgnoreCase) > -1;

        return containsIt;
    }

    public static string ToPlatformPath(this string input)
    {
        return input.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
    }

    public static string ToUnixPath(this string input)
    {
        return input.Replace('\\', '/');
    }

    public static string NormalizePath(this string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static string ToPathKey(this string path)
    {
        var normalized = path.NormalizePath();
        return OperatingSystem.IsWindows() ? normalized.ToLowerInvariant() : normalized;
    }

    public static bool IsValidFileName(this string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var chars = Path.GetInvalidFileNameChars();
        foreach (var character in chars)
            if (name.Contains(character))
                return false;
        return true;
    }


    public static string CheckNameFile(this string fullPath)
    {
        var folderPath = Path.GetDirectoryName(fullPath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "unknown";
        var extension = Path.GetExtension(fullPath) ?? "";
        for (var i = 1; File.Exists(fullPath); i++) fullPath = Path.Combine(folderPath, fileName + i + extension);
        return fullPath;
    }

    public static string CheckNameDirectory(this string fullPath)
    {
        var folderPath = Path.GetDirectoryName(fullPath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "unknown";
        for (var i = 1; Directory.Exists(fullPath); i++) fullPath = Path.Combine(folderPath, fileName + i);
        return fullPath;
    }
}
