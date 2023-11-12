using System.Text.RegularExpressions;
using OneWare.Shared.Models;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.Vhdl.Parsing;

public class VhdlNodeProvider : INodeProvider
{
    public IEnumerable<NodeModel> ExtractNodes(IProjectFile file)
    {
        var fileLines = File.ReadAllLines(file.FullPath);
        
        bool inEntity = false;
        foreach (var line in fileLines)
        {
            // Check for entity start
            if (Regex.IsMatch(line, @"\bentity\b", RegexOptions.IgnoreCase))
            {
                inEntity = true;
                continue;
            }

            // Check for entity end
            if (inEntity && Regex.IsMatch(line, @"\bend entity\b", RegexOptions.IgnoreCase))
            {
                inEntity = false;
                continue;
            }

            // Extract inputs and outputs
            if (inEntity)
            {
                MatchCollection matches = Regex.Matches(line, @"\b(\w+)\s*:\s*(in|out)\s*\w+", RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    string portName = match.Groups[1].Value;
                    string direction = match.Groups[2].Value;
                    
                    yield return new NodeModel(portName);
                }
            }
        }
    }
}