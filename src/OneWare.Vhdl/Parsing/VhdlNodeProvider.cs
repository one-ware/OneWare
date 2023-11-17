using System.Text.RegularExpressions;
using OneWare.Shared.Models;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Vhdl.Parsing;

public class VhdlNodeProvider : INodeProvider
{
    public IEnumerable<NodeModel> ExtractNodes(IProjectFile file)
    {
        var fileLines = File.ReadAllLines(file.FullPath);
        return ExtractEntityPorts(fileLines);
    }

    public static IEnumerable<NodeModel> ExtractEntityPorts(IEnumerable<string> vhdlLines)
    {
        bool inPortSection = false;
        var portPattern = @"\b(\w+)\s+:\s+(in|out|inout|buffer)\s+(\w+)(?:\((\d+)\s+downto\s+(\d+)\))?(?:\s+:=\s+[^;]+)?";

        foreach (var line in vhdlLines)
        {
            if (line.Trim().StartsWith("port (", StringComparison.OrdinalIgnoreCase)) inPortSection = true;
            if (line.Trim().StartsWith(");", StringComparison.OrdinalIgnoreCase)) inPortSection = false;

            if (inPortSection)
            {
                var match = Regex.Match(line, portPattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var portName = match.Groups[1].Value;
                    var direction = match.Groups[2].Value;
                    var portType = match.Groups[3].Value;
                    var upperBound = match.Groups[4].Value;
                    var lowerBound = match.Groups[5].Value;

                    if (!string.IsNullOrEmpty(upperBound) && !string.IsNullOrEmpty(lowerBound))
                    {
                        // Expand std_logic_vector into individual ports
                        int upper = int.Parse(upperBound);
                        int lower = int.Parse(lowerBound);

                        for (var i = upper; i >= lower; i--)
                        {
                            yield return new NodeModel($"{portName}[{i}]", direction); //$"{portName}({i}) : {direction} {portType}";
                        }
                    }
                    else
                    {
                        // Single port
                        yield return new NodeModel(portName, direction); // $"{portName} : {direction} {portType}";
                    }
                }
            }
        }
    }
}