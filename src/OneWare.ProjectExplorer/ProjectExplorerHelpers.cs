namespace OneWare.ProjectExplorer;

public static class ProjectExplorerHelpers
{
    public static string CheckNameFile(string fullPath)
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
        
    public static string CheckNameDirectory(string fullPath)
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