using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Vhdl.Parsing;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class VhdlNodeProvider : INodeProvider
{
    public const string NodeProviderKey = "VHDL_Basic";

    public string Name => NodeProviderKey;

    public string[] SupportedLanguages => ["VHDL"];

    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        var code = File.ReadAllText(file.FullPath);
        return Task.FromResult<IEnumerable<FpgaNode>>(ExtractNodes(code));
    }

    private static List<FpgaNode> ExtractNodes(string vhdlCode)
    {
        var nodes = new List<FpgaNode>();

        // ✅ Strip comments FIRST
        var cleanCode = StripComments(vhdlCode);

        // Extract generics
        var generics = ExtractGenerics(cleanCode);

        // Extract ports
        var portContent = ExtractBlock(cleanCode, "port");
        if (!string.IsNullOrEmpty(portContent))
            ExtractPorts(portContent, nodes, generics);

        return nodes;
    }

    // ----------------- COMMENT STRIPPING -----------------
    private static string StripComments(string code)
    {
        var sb = new StringBuilder(code.Length);
        using var reader = new StringReader(code);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var commentIndex = line.IndexOf("--", StringComparison.Ordinal);
            if (commentIndex >= 0)
                sb.AppendLine(line.Substring(0, commentIndex));
            else
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    // ----------------- SAFE BLOCK EXTRACTION -----------------
    private static string? ExtractBlock(string code, string keyword)
    {
        var keywordIndex = code.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (keywordIndex == -1)
            return null;

        var start = code.IndexOf('(', keywordIndex);
        if (start == -1)
            return null;

        var depth = 0;
        for (var i = start; i < code.Length; i++)
            if (code[i] == '(')
            {
                depth++;
            }
            else if (code[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    var end = i;
                    return code.Substring(start + 1, end - start - 1);
                }
            }

        return code.Substring(start + 1);
    }

    // ----------------- GENERIC EXTRACTION -----------------
    private static Dictionary<string, int> ExtractGenerics(string vhdlCode)
    {
        var genericValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var genericContent = ExtractBlock(vhdlCode, "generic");
        if (string.IsNullOrEmpty(genericContent))
            return genericValues;

        var genericDeclarations = GenericDeclarationMatch().Matches(genericContent);
        foreach (Match match in genericDeclarations)
        {
            var name = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();
            if (int.TryParse(value, out var intValue))
                genericValues[name] = intValue;
        }

        return genericValues;
    }

    // ----------------- PORT EXTRACTION -----------------
    private static void ExtractPorts(
        string portContent,
        List<FpgaNode> nodes,
        Dictionary<string, int> genericValues)
    {
        var portDeclarations = portContent.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var decl in portDeclarations)
        {
            var declaration = decl.Trim();
            if (string.IsNullOrWhiteSpace(declaration)) continue;

            var vectorMatch = VectorMatch().Match(declaration);
            var logicMatch = LogicMatch().Match(declaration);

            // ---------------- VECTOR PORTS ----------------
            if (vectorMatch.Success)
            {
                var namesPart = vectorMatch.Groups[1].Value.Trim();
                var direction = vectorMatch.Groups[2].Value.ToUpper().Trim();
                var upperBoundExpr = vectorMatch.Groups[3].Value.Trim();
                var directionType = vectorMatch.Groups[4].Value.Trim();
                var lowerBoundExpr = vectorMatch.Groups[5].Value.Trim();

                var names = namesPart.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var upper = EvaluateExpression(upperBoundExpr, genericValues);
                var lower = EvaluateExpression(lowerBoundExpr, genericValues);

                foreach (var name in names)
                    if (directionType.Equals("to", StringComparison.OrdinalIgnoreCase))
                        for (var i = upper; i <= lower; i++)
                            nodes.Add(new FpgaNode($"{name}[{i}]", direction));
                    else // downto
                        for (var i = upper; i >= lower; i--)
                            nodes.Add(new FpgaNode($"{name}[{i}]", direction));
            }
            // ---------------- SCALAR PORTS ----------------
            else if (logicMatch.Success)
            {
                var namesPart = logicMatch.Groups[1].Value.Trim();
                var direction = logicMatch.Groups[2].Value.ToUpper().Trim();

                var names = namesPart.Split(',',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var name in names)
                    nodes.Add(new FpgaNode(name, direction));
            }
        }
    }

    // ----------------- EXPRESSION EVALUATION -----------------
    private static int EvaluateExpression(string expr, Dictionary<string, int> generics)
    {
        foreach (var g in generics.OrderByDescending(x => x.Key.Length))
            expr = Regex.Replace(
                expr,
                $@"\b{g.Key}\b",
                g.Value.ToString(),
                RegexOptions.IgnoreCase);

        expr = expr.Replace(" ", "");

        try
        {
            var dt = new DataTable();
            var result = dt.Compute(expr, "");
            return Convert.ToInt32(result);
        }
        catch
        {
            throw new ArgumentException($"Unable to evaluate expression: {expr}");
        }
    }

    // ----------------- REGEX DEFINITIONS -----------------
    [GeneratedRegex(
        @"([\w\s,]+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC(?:\s*:=\s*'[01]')?",
        RegexOptions.IgnoreCase)]
    private static partial Regex LogicMatch();

    [GeneratedRegex(
        @"([\w\s,]+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC_VECTOR\s*\(\s*([^)]+?)\s*(to|downto)\s*([^)]+)\s*\)(?:\s*:=\s*[^;]+)?",
        RegexOptions.IgnoreCase)]
    private static partial Regex VectorMatch();

    [GeneratedRegex(
        @"(\w+)\s*:\s*\w+\s*:=\s*(\d+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex GenericDeclarationMatch();
}