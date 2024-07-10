namespace OneWare.Essentials.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveWhitespace(this string input)
        {
            return new string(input.ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());
        }

        public static bool EqualPaths(this string input, string otherPath)
        {
            if(string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(otherPath)) return input.Trim() == otherPath.Trim();
            return Path.GetFullPath(input).TrimEnd('\\').Equals(Path.GetFullPath(otherPath).TrimEnd('\\'),
                StringComparison.InvariantCultureIgnoreCase);
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
            return Path.GetFullPath(new Uri(path).LocalPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
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
            for (var i = 1; File.Exists(fullPath); i++)
            {
                fullPath = Path.Combine(folderPath, fileName + i + extension);
            }
            return fullPath;
        }
        
        public static string CheckNameDirectory(this string fullPath)
        {
            var folderPath = Path.GetDirectoryName(fullPath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(fullPath) ?? "unknown";
            for (var i = 1; Directory.Exists(fullPath); i++)
            {
                fullPath = Path.Combine(folderPath, fileName + i);
            }
            return fullPath;
        }
    }
}