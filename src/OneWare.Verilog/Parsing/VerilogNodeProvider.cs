using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Parsing;

public class VerilogNodeProvider : INodeProvider
{
    public IEnumerable<FpgaNode> ExtractNodes(IProjectFile file)
    {
        var fileContent = File.ReadAllText(file.FullPath);

        // Regex, um die Modul-Deklaration zu finden und die Ports zu extrahieren
        var modulePattern = @"module\s+\w+\s*\((.*?)\);";
        var moduleMatch = Regex.Match(fileContent, modulePattern, RegexOptions.Singleline);

        if (moduleMatch.Success)
        {
            // Extrahieren der Ports innerhalb des Moduls
            var portSection = moduleMatch.Groups[1].Value;
            return ExtractAndPrintPorts(portSection);
        }

        return new List<FpgaNode>();
    }

    private static IEnumerable<FpgaNode> ExtractAndPrintPorts(string portSection)
    {
        // Regex, um einzelne Port-Deklarationen zu identifizieren
        var portPattern = @"(input|output|inout)(\s*\[\d+:\d+\])?\s+([^;,]+)";
        var portMatches = Regex.Matches(portSection, portPattern, RegexOptions.Singleline);

        foreach (Match match in portMatches)
        {
            var portType = match.Groups[1].Value;
            var vectorSize = match.Groups[2].Value.Trim();
            var portNames = match.Groups[3].Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var portName in portNames)
                if (!string.IsNullOrWhiteSpace(vectorSize))
                {
                }
                else
                {
                    yield return new FpgaNode(portName.Trim(), portType);
                }
        }
    }
}