using OneWare.Essentials.Helpers;

namespace OneWare.UniversalFpgaProjectSystem.Helpers;

public static class TemplateHelper
{
    public static void CopyDirectoryAndReplaceString(string sourcePath, string destPath,
        params (string source, string replacement)[] replacements)
    {
        var dir = new DirectoryInfo(sourcePath);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destPath);

        foreach (var file in dir.GetFiles())
        {
            var fileName = file.Name;
            foreach (var (source, replacement) in replacements) fileName = fileName.Replace(source, replacement);
            var targetFilePath = Path.Combine(destPath, fileName);
            var fileString = File.ReadAllText(file.FullName);
            foreach (var (source, replacement) in replacements) fileString = fileString.Replace(source, replacement);
            File.WriteAllText(targetFilePath, fileString);
            PlatformHelper.ChmodFile(targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destPath, subDir.Name);
            CopyDirectoryAndReplaceString(subDir.FullName, newDestinationDir, replacements);
        }
    }
}