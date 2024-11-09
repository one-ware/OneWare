using System.Data;
using System.Text.RegularExpressions;
using OneWare.Essentials.Models;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.Vhdl.Parsing;

// ReSharper disable once ClassNeverInstantiated.Global
public partial class VhdlNodeProvider : INodeProvider
{
    public IEnumerable<FpgaNode> ExtractNodes(IProjectFile file)
    {
        var code = File.ReadAllText(file.FullPath);
        return ExtractNodes(code);
    }

    private static List<FpgaNode> ExtractNodes(string vhdlCode)
    {
        var nodes = new List<FpgaNode>();
        
        // First, extract generics
        var generics = ExtractGenerics(vhdlCode);

        // Extract port declarations
        ExtractPorts(vhdlCode, nodes, generics);

        return nodes;
    }

    private static Dictionary<string, int> ExtractGenerics(string vhdlCode)
    {
        var genericValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        var genericMatch = GenericMatch().Match(vhdlCode);
        if (genericMatch.Success)
        {
            var genericContent = genericMatch.Groups[1].Value;
            var genericDeclarations = GenericDeclarationMatch().Matches(genericContent);
            
            foreach (Match match in genericDeclarations)
            {
                var name = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                if (int.TryParse(value, out int intValue))
                {
                    genericValues[name] = intValue;
                }
            }
        }

        return genericValues;
    }

    private static void ExtractPorts(string vhdlCode, List<FpgaNode> nodes, Dictionary<string, int> genericValues)
    {
        var portMatch = PortMatch().Match(vhdlCode);
        if (!portMatch.Success) return;

        var portContent = portMatch.Groups[1].Value;
        var portDeclarations = portContent.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var declaration in portDeclarations)
        {
            if (string.IsNullOrWhiteSpace(declaration)) continue;

            // Match for vector declarations
            var vectorMatch = VectorMatch().Match(declaration);

            // Match for single std_logic declarations
            var logicMatch = LogicMatch().Match(declaration);

            if (vectorMatch.Success)
            {
                var name = vectorMatch.Groups[1].Value.Trim();
                var direction = vectorMatch.Groups[2].Value.ToUpper().Trim();
                var upperBoundExpr = vectorMatch.Groups[3].Value.Trim();
                var lowerBoundExpr = vectorMatch.Groups[4].Value.Trim();

                try
                {
                    var upperBound = EvaluateExpression(upperBoundExpr, genericValues);
                    var lowerBound = EvaluateExpression(lowerBoundExpr, genericValues);

                    // Create nodes for each bit in the vector
                    if (upperBound >= lowerBound)
                    {
                        for (var i = lowerBound; i <= upperBound; i++)
                        {
                            nodes.Add(new FpgaNode($"{name}[{i}]", direction));
                        }
                    }
                    else
                    {
                        for (var i = lowerBound; i >= upperBound; i--)
                        {
                            nodes.Add(new FpgaNode($"{name}[{i}]", direction));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing vector {name}: {ex.Message}");
                }
            }
            else if (logicMatch.Success)
            {
                var name = logicMatch.Groups[1].Value.Trim();
                var direction = logicMatch.Groups[2].Value.ToUpper().Trim();
                nodes.Add(new FpgaNode(name, direction));
            }
        }
    }

    private static int EvaluateExpression(string expression, Dictionary<string, int> genericValues)
    {
        // Replace generic constants with their values
        foreach (var generic in genericValues.OrderByDescending(x => x.Key.Length))
        {
            expression = Regex.Replace(expression, 
                $@"\b{generic.Key}\b", 
                generic.Value.ToString(), 
                RegexOptions.IgnoreCase);
        }

        // Clean up the expression
        expression = expression.Replace(" ", "");

        try
        {
            var dt = new DataTable();
            var result = dt.Compute(expression, "");
            return Convert.ToInt32(result);
        }
        catch (Exception)
        {
            throw new ArgumentException($"Unable to evaluate expression: {expression}");
        }
    }

    [GeneratedRegex(@"(\w+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC(?:\s*:=\s*'[01]')?", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex LogicMatch();
    
    [GeneratedRegex(@"port\s*\(((?:[^()]*|\((?:[^()]*|\([^()]*\))*\))*)\)\s*;", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
    private static partial Regex PortMatch();
    
    [GeneratedRegex(@"Generic\s*\((.*?)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline, "en-US")]
    private static partial Regex GenericMatch();
    
    [GeneratedRegex(@"(\w+)\s*:\s*\w+\s*:=\s*(\d+)")]
    private static partial Regex GenericDeclarationMatch();
    
    [GeneratedRegex(@"(\w+)\s*:\s*(IN|OUT|INOUT|BUFFER)\s*STD_LOGIC_VECTOR\s*\(\s*([^)]+)\s*downto\s*([^)]+)\s*\)(?:\s*:=\s*[^;]+)?", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex VectorMatch();
}