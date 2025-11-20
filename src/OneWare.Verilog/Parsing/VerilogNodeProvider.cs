using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Verilog.Parsing
{
    public class VerilogNodeProvider : INodeProvider
    {
        public string Name => "Verilog_Basic";

        public string[] SupportedLanguages => ["Verilog"];
        
        // Strip /* ... */ block comments
        private static readonly Regex BlockCommentRegex = new(
            @"/\*.*?\*/",
            RegexOptions.Singleline | RegexOptions.Compiled);

        // Strip // line comments
        private static readonly Regex LineCommentRegex = new(
            @"//.*?$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// THIS IS THE FIX.
        /// Do NOT allow commas that cross newlines.
        /// </summary>
        private static readonly Regex PortDeclarationRegex = new(
            @"\b(?<dir>input|output|inout)\b" +
            @"(?<types>(?:\s+\b(?:wire|reg|logic|signed|tri|tri0|tri1|bit|byte|shortint|int|longint|real|realtime|time)\b)*)" +
            @"[ \t]*(?<range>\[[^]]+\])?" +
            @"[ \t]*(?<names>[\w$]+(?:[ \t]*,[ \t]*[\w$]+)*)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.FullPath) || !File.Exists(file.FullPath))
                return Task.FromResult<IEnumerable<FpgaNode>>(Array.Empty<FpgaNode>());

            string fileContent = File.ReadAllText(file.FullPath);
            string cleaned = RemoveComments(fileContent);

            var result = new List<FpgaNode>();

            var modules = ParseModules(cleaned);
            foreach (var (moduleName, portHeader, body) in modules)
            {
                var ports = new Dictionary<string, (string direction, string? range)>(StringComparer.Ordinal);

                // ANSI-style ports in header
                ExtractPortsFromText(portHeader, ports);

                // Classic-style ports in body
                ExtractPortsFromText(body, ports);

                // Create nodes & expand vectors
                foreach (var kvp in ports)
                {
                    string portName = kvp.Key;
                    var (direction, range) = kvp.Value;

                    foreach (var expanded in ExpandPort(portName, range))
                        result.Add(new FpgaNode(expanded, direction));
                }
            }

            return Task.FromResult<IEnumerable<FpgaNode>>(result);
        }

        private static string RemoveComments(string text)
        {
            text = BlockCommentRegex.Replace(text, string.Empty);
            text = LineCommentRegex.Replace(text, string.Empty);
            return text;
        }

        /// <summary>
        /// Balanced and safe module parser (replaces broken regex attempts).
        /// </summary>
        private static IEnumerable<(string name, string ports, string body)> ParseModules(string text)
        {
            var list = new List<(string, string, string)>();
            int index = 0;

            while (true)
            {
                int moduleIndex = text.IndexOf("module", index, StringComparison.Ordinal);
                if (moduleIndex < 0)
                    break;

                var nameMatch = Regex.Match(text.Substring(moduleIndex),
                    @"\bmodule\s+([A-Za-z_]\w*)");
                if (!nameMatch.Success)
                    break;

                string moduleName = nameMatch.Groups[1].Value;
                int pos = moduleIndex + nameMatch.Length;

                // Skip whitespace
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                    pos++;

                // Optional parameter list #( ...)
                if (pos < text.Length && text[pos] == '#')
                {
                    int start = text.IndexOf('(', pos + 1);
                    if (start < 0) break;

                    int depth = 1;
                    int i = start + 1;
                    while (i < text.Length && depth > 0)
                    {
                        if (text[i] == '(') depth++;
                        else if (text[i] == ')') depth--;
                        i++;
                    }

                    if (depth != 0) break;

                    pos = i;
                }

                // Now expect port list '('
                if (pos >= text.Length || text[pos] != '(')
                {
                    index = moduleIndex + 6;
                    continue;
                }

                int portStart = pos;
                int d = 1;
                int j = portStart + 1;

                while (j < text.Length && d > 0)
                {
                    if (text[j] == '(') d++;
                    else if (text[j] == ')') d--;
                    j++;
                }

                if (d != 0)
                    break;

                string portText = text.Substring(portStart + 1, j - portStart - 2);

                // header ends with semicolon
                int semi = text.IndexOf(";", j);
                if (semi < 0)
                    break;

                int bodyStart = semi + 1;
                int endmoduleIndex = text.IndexOf("endmodule", bodyStart, StringComparison.Ordinal);
                if (endmoduleIndex < 0)
                    break;

                string bodyText = text.Substring(bodyStart, endmoduleIndex - bodyStart);

                list.Add((moduleName, portText, bodyText));

                index = endmoduleIndex + 9;
            }

            return list;
        }

        /// <summary>
        /// Extract input/output/inout declarations (ANSI + classic).
        /// </summary>
        private static void ExtractPortsFromText(
            string text,
            IDictionary<string, (string direction, string? range)> ports)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var matches = PortDeclarationRegex.Matches(text);
            foreach (Match m in matches)
            {
                string direction = m.Groups["dir"].Value;
                string range = m.Groups["range"].Value;
                string namesRaw = m.Groups["names"].Value;

                var names = namesRaw
                    .Split(',')
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0);

                foreach (string name in names)
                {
                    // HARD SAFETY: do NOT treat keywords as names
                    if (name is "input" or "output" or "inout")
                        continue;

                    if (!ports.ContainsKey(name))
                        ports[name] = (direction, string.IsNullOrEmpty(range) ? null : range);
                }
            }
        }

        /// <summary>
        /// Expand numeric vectors. Symbolic vectors remain as one name.
        /// </summary>
        private static IEnumerable<string> ExpandPort(string baseName, string? range)
        {
            if (string.IsNullOrWhiteSpace(range))
                return new[] { baseName };

            var m = Regex.Match(range, @"\[(?<a>-?\d+)\s*:\s*(?<b>-?\d+)\]");
            if (!m.Success)
                return new[] { baseName }; // symbolic → no expansion

            int a = int.Parse(m.Groups["a"].Value);
            int b = int.Parse(m.Groups["b"].Value);

            int from = Math.Min(a, b);
            int to = Math.Max(a, b);

            return Enumerable.Range(from, to - from + 1)
                .Select(i => $"{baseName}[{i}]");
        }
    }
}
    
    

