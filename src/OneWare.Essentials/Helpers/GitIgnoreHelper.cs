namespace OneWare.Essentials.Helpers;

public class GitIgnoreHelper
{
    public static void AddToGitIgnore(string path, string line)
    {
        var gitIgnorePath = Path.Combine(path, ".gitignore");
        
        if (File.Exists(gitIgnorePath))
        {
            var lines = File.ReadAllLines(gitIgnorePath);
            
            if (!lines.Contains(line))
            {
                File.AppendAllText(gitIgnorePath, line + "\n");
            }
        }
        else
        {
            File.WriteAllText(gitIgnorePath, line + "\n");
        }
    }
}