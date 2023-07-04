namespace OneWare.Shared.Extensions
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

        public static string ToLinuxPath(this string input)
        {
            return input.Replace('\\', '/');
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