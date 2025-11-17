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
    public Task<IEnumerable<FpgaNode>> ExtractNodesAsync(IProjectFile file)
    {
        var code = File.ReadAllText(file.FullPath);
        return Task.FromResult<IEnumerable<FpgaNode>>(ExtractNodes(code));
    }

    
    public string GetDisplayName()
    {
        return "Basic VHDLNodeProvider";
    }

    public string GetKey()
    {
        return "BasicVHDLNodeProvider";
    }

    private static List<FpgaNode> ExtractNodes(string vhdlCode)
    {
        var nodes = new List<FpgaNode>();

        // Extract generics first (safe)
        var generics = ExtractGenerics(vhdlCode);

        // Extract ports safely (no regex recursion)
        var portContent = ExtractBlock(vhdlCode, "port");
        if (!string.IsNullOrEmpty(portContent))
            ExtractPorts(portContent, nodes, generics);

        return nodes;
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

        int depth = 0;
        for (int i = start; i < code.Length; i++)
        {
            if (code[i] == '(') depth++;
            else if (code[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    // Extract content inside parentheses
                    var end = i;
                    return code.Substring(start + 1, end - start - 1);
                }
            }
        }

        // Unbalanced parentheses -> return until end of file
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
            if (int.TryParse(value, out int intValue))
                genericValues[name] = intValue;
        }

        return genericValues;
    }

    // ----------------- PORT EXTRACTION -----------------
    private static void ExtractPorts(string portContent, List<FpgaNode> nodes, Dictionary<string, int> genericValues)
    {
        var portDeclarations = portContent.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var decl in portDeclarations)
        {
            var declaration = decl.Trim();
            if (string.IsNullOrWhiteSpace(declaration)) continue;

            // Match vector first
            var vectorMatch = VectorMatch().Match(declaration);
            // Match scalar logic
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

                try
                {
                    var upper = EvaluateExpression(upperBoundExpr, genericValues);
                    var lower = EvaluateExpression(lowerBoundExpr, genericValues);

                    foreach (var name in names)
                    {
                        if (directionType.Equals("to", StringComparison.OrdinalIgnoreCase))
                        {
                            for (int i = upper; i <= lower; i++)
                                nodes.Add(new FpgaNode($"{name}[{i}]", direction));
                        }
                        else if (directionType.Equals("downto", StringComparison.OrdinalIgnoreCase))
                        {
                            for (int i = upper; i >= lower; i--)
                                nodes.Add(new FpgaNode($"{name}[{i}]", direction));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing vector {namesPart}: {ex.Message}");
                }
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
        {
            expr = Regex.Replace(expr, $@"\b{g.Key}\b", g.Value.ToString(), RegexOptions.IgnoreCase);
        }

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
    [GeneratedRegex(@"([\w\s,]+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC(?:\s*:=\s*'[01]')?", RegexOptions.IgnoreCase)]
    private static partial Regex LogicMatch();

    [GeneratedRegex(@"([\w\s,]+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC_VECTOR\s*\(\s*([^)]+?)\s*(to|downto)\s*([^)]+)\s*\)(?:\s*:=\s*[^;]+)?", RegexOptions.IgnoreCase)]
    private static partial Regex VectorMatch();

    [GeneratedRegex(@"(\w+)\s*:\s*\w+\s*:=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GenericDeclarationMatch();
}