namespace OneWare.OssCadSuiteIntegration.Tools;

public class ConstraintFileHelper
{
    public static void Convert(string pcfPath, string ccfPath)
    {
        if (!File.Exists(pcfPath))
            throw new FileNotFoundException("PCF file not found", pcfPath);

        var result = new List<string>();

        var lines = File.ReadAllLines(pcfPath);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            
            if (string.IsNullOrWhiteSpace(line))
            {
                result.Add(string.Empty);
                continue;
            }

            if (line.StartsWith("#"))
            {
                result.Add(line);
                continue;
            }

            if (!line.StartsWith("set_io"))
                continue;
            
            var commentIndex = line.IndexOf('#');
            var comment = commentIndex >= 0 ? line[commentIndex..] : string.Empty;

            var pureLine = commentIndex >= 0
                ? line[..commentIndex].Trim()
                : line;

            var parts = pureLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
                continue;

            var signal = parts[1];
            var pin = parts[2];

            var newLine = $"NET \"{signal}\" Loc = \"{pin}\";";

            if (!string.IsNullOrEmpty(comment))
                newLine += $" {comment}";

            result.Add(newLine);
        }

        File.WriteAllLines(ccfPath, result);
    }
}