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
    
    public static void ConvertCcfToPcf(string ccfPath, string pcfPath)
    {
    if (!File.Exists(ccfPath))
        throw new FileNotFoundException("CCF file not found", ccfPath);

    var result = new List<string>();
    var lines = File.ReadAllLines(ccfPath);

    foreach (var rawLine in lines)
    {
        var line = rawLine.Trim();
        
        if (string.IsNullOrWhiteSpace(line))
        {
            result.Add(string.Empty);
            continue;
        }

        // Kommentare in CCF können mit # oder // starten
        if (line.StartsWith("#") || line.StartsWith("//"))
        {
            result.Add(line);
            continue;
        }

        // CCF-Zeilen beginnen typischerweise mit NET
        if (!line.StartsWith("NET", StringComparison.OrdinalIgnoreCase))
            continue;
        
        // Kommentar am Ende der Zeile wegschneiden
        var commentIndex = line.IndexOf('#');
        if (commentIndex < 0) commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        
        var comment = commentIndex >= 0 ? line[commentIndex..] : string.Empty;
        var pureLine = commentIndex >= 0 ? line[..commentIndex].Trim() : line;

        // Semikolon am Ende entfernen, falls vorhanden
        if (pureLine.EndsWith(";"))
            pureLine = pureLine[..^1].Trim();

        // Extraktion von Signal und Pin mittels Regex oderhelfer (wir nutzen hier einfaches Splitten/Replace)
        // Beispiel: NET "my_signal" Loc = "P1";
        var parts = pureLine.Split(new[] { ' ', '=' }, StringSplitOptions.RemoveEmptyEntries);

        // Erwartete Fragmente nach dem Splitten: ["NET", "\"signal\"", "Loc", "\"pin\""]
        if (parts.Length < 4 || !parts[2].Equals("Loc", StringComparison.OrdinalIgnoreCase))
            continue;

        // Anführungszeichen entfernen
        var signal = parts[1].Replace("\"", "");
        var pin = parts[3].Replace("\"", "");

        var newLine = $"set_io {signal} {pin}";

        if (!string.IsNullOrEmpty(comment))
            newLine += $" {comment}";

        result.Add(newLine);
    }

    File.WriteAllLines(pcfPath, result);
}
}