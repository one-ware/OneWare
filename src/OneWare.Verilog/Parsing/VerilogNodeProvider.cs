using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Parsing;

public class VerilogNodeProvider : INodeProvider
{
    
    // Strip /* */ comments
    private static readonly Regex BlockCommentRegex = new(
        @"/\*.*?\*/",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Strip // comments
    private static readonly Regex LineCommentRegex = new(
        @"//.*?$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Matches a Verilog module with optional parameter list, port list and body.
    /// - name: module name
    /// - ports: raw port header text (inside parentheses)
    /// - body: everything until 'endmodule'
    /// </summary>
    private static readonly Regex ModuleRegex = new(
        @"\bmodule\s+(?<name>\w+)\s*" +
        @"(?<params>#\s*\([^;]*\))?\s*" +
        @"\((?<ports>[^;]*?)\)\s*;" +
        @"(?<body>.*?)(?=\bendmodule\b)",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Matches ANSI-style / classic port declarations like:
    ///   input clk;
    ///   input [7:0] data_in, data_in2;
    ///   output reg [7:0] data_out;
    /// </summary>
    private static readonly Regex PortDeclarationRegex = new(
        @"\b(input|output|inout)\b" +                // direction
        @"\s*(?:\bwire\b|\breg\b|\blogic\b|\bsigned\b|\btri\b|\btri0\b|\btri1\b|\bbit\b|\bbyte\b|\bshortint\b|\bint\b|\blongint\b|\breal\b|\brealtime\b|\btime\b)?"
        + @"\s*(\[[^]]+\])?"                         // optional range [msb:lsb]
        + @"\s*(?<names>[\w$]+(?:\s*,\s*[\w$]+)*)",  // one or more names, comma separated
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled);

    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        if (file == null || string.IsNullOrWhiteSpace(file.FullPath) || !File.Exists(file.FullPath))
            return Task.FromResult<IEnumerable<FpgaNode>>(Array.Empty<FpgaNode>());

        var fileContent = File.ReadAllText(file.FullPath);

        // Remove comments so they don't break simple regex parsing
        var cleaned = RemoveComments(fileContent);

        var result = new List<FpgaNode>();

        var moduleMatches = ModuleRegex.Matches(cleaned);
        if (moduleMatches.Count == 0)
            return Task.FromResult<IEnumerable<FpgaNode>>(result);

        foreach (Match moduleMatch in moduleMatches)
        {
            var moduleName = moduleMatch.Groups["name"].Value;
            var portHeader = moduleMatch.Groups["ports"].Value;
            var body = moduleMatch.Groups["body"].Value;

            // First, extract *all* port names that appear in the header – regardless of direction.
            // This list is used to filter body declarations so we don't accidentally treat
            // function/task arguments as top-level module ports.
            var headerPortNames = ExtractHeaderPortNames(portHeader);

            // direction dictionary: name -> direction (input/output/inout)
            var ports = new Dictionary<string, string>(StringComparer.Ordinal);

            // 1. ANSI-style: directions declared in module header
            ExtractPortsFromText(portHeader, ports, headerPortNamesFilter: null);

            // 2. Classic style: directions often declared in the body.
            //    Here we *only* accept names that are present in the header name list.
            ExtractPortsFromText(body, ports, headerPortNames);

            // Finally, create nodes
            foreach (var kvp in ports)
            {
                var portName = kvp.Key;
                var direction = kvp.Value; // "input", "output", "inout"

                if (string.IsNullOrWhiteSpace(portName))
                    continue;

                result.Add(new FpgaNode(portName, direction)
                {
                    // If FpgaNode supports more metadata (like module name), you could set it here.
                    // For now we only pass name + direction like the original implementation.
                });
            }
        }

        return  Task.FromResult<IEnumerable<FpgaNode>>(result);
    }

    private static string RemoveComments(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var noBlock = BlockCommentRegex.Replace(text, string.Empty);
        var noLine = LineCommentRegex.Replace(noBlock, string.Empty);
        return noLine;
    }
    
    public string GetDisplayName()
    {
        return "Basic VerilogNodeProvider";
    }
    
    public string GetKey()
    {
        return "VerilogNodeProvider";
    }

    /// <summary>
    /// Extracts all potential port names that appear in the module header list, regardless of direction.
    /// Handles both ANSI-style and non-ANSI (just a name list).
    /// </summary>
    private static HashSet<string> ExtractHeaderPortNames(string portHeader)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(portHeader))
            return result;

        // tokens to ignore as "names"
        var keywords = new HashSet<string>(new[]
        {
            "input", "output", "inout",
            "wire", "reg", "logic", "signed",
            "tri", "tri0", "tri1", "bit",
            "byte", "shortint", "int", "longint",
            "real", "realtime", "time",
            "module", "parameter", "localparam"
        }, StringComparer.Ordinal);

        var matches = Regex.Matches(portHeader, @"\b([\w$]+)\b");
        foreach (Match match in matches)
        {
            var token = match.Groups[1].Value;
            if (!keywords.Contains(token))
            {
                result.Add(token);
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts ports from a piece of Verilog text using PortDeclarationRegex.
    /// If headerPortNamesFilter is not null, only ports whose names are in that set are added.
    /// This lets us parse the body but only keep declarations that correspond to the module header.
    /// </summary>
    private static void ExtractPortsFromText(
        string text,
        IDictionary<string, string> ports,
        HashSet<string>? headerPortNamesFilter)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var matches = PortDeclarationRegex.Matches(text);
        foreach (Match match in matches)
        {
            var direction = match.Groups[1].Value;      // input/output/inout
            // var range = match.Groups[2].Value;      // [msb:lsb] currently unused, but could be stored later
            var namesGroup = match.Groups["names"].Value;

            if (string.IsNullOrWhiteSpace(namesGroup))
                continue;

            var names = namesGroup
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n));

            foreach (var name in names)
            {
                if (headerPortNamesFilter != null && !headerPortNamesFilter.Contains(name))
                    continue; // avoid picking up local signals / function args

                // Don't overwrite existing direction if we've already seen this port (e.g. from header).
                if (!ports.ContainsKey(name))
                    ports[name] = direction;
            }
        }
    }
}