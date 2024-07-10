using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Parsing;

public class VerilogNodeProvider : INodeProvider
{
    public IEnumerable<FpgaNode> ExtractNodes(IProjectFile file)
    {
        string fileContent = File.ReadAllText(file.FullPath);

        // Regex, um die Modul-Deklaration zu finden und die Ports zu extrahieren
        string modulePattern = @"module\s+\w+\s*\((.*?)\);";
        Match moduleMatch = Regex.Match(fileContent, modulePattern, RegexOptions.Singleline);

        if (moduleMatch.Success)
        {
            // Extrahieren der Ports innerhalb des Moduls
            string portSection = moduleMatch.Groups[1].Value;
            return ExtractAndPrintPorts(portSection);
        }
        return new List<FpgaNode>();
    }

    private static IEnumerable<FpgaNode> ExtractAndPrintPorts(string portSection)
    {
        // Regex, um einzelne Port-Deklarationen zu identifizieren
        string portPattern = @"(input|output|inout)(\s*\[\d+:\d+\])?\s+([^;,]+)";
        MatchCollection portMatches = Regex.Matches(portSection, portPattern, RegexOptions.Singleline);

        foreach (Match match in portMatches)
        {
            string portType = match.Groups[1].Value;
            string vectorSize = match.Groups[2].Value.Trim();
            string[] portNames = match.Groups[3].Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string portName in portNames)
            {
                if (!string.IsNullOrWhiteSpace(vectorSize))
                {
                    
                }
                else yield return new FpgaNode(portName.Trim(), portType);
            }
        }
    }
}