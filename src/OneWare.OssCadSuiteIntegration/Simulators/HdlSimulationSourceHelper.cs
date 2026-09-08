using System.Text.RegularExpressions;
using OneWare.Essentials.Extensions;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.OssCadSuiteIntegration.Simulators;

internal static partial class HdlSimulationSourceHelper
{
    public static List<string> GetOrderedSources(UniversalFpgaProjectRoot root, string activeTestBenchPath)
    {
        var activeTestBenchRelative = Path.GetRelativePath(root.FullPath, activeTestBenchPath).ToUnixPath();

        return root.GetFiles()
            .Where(x => Path.GetExtension(x) is ".v" or ".sv")
            .Where(x => !root.IsCompileExcluded(x))
            .Where(x => !root.IsTestBench(x) || x.EqualPaths(activeTestBenchRelative))
            .OrderBy(x => DeclaresPackage(root, x) ? 0 : 1)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.ToUnixPath())
            .ToList();
    }

    private static bool DeclaresPackage(UniversalFpgaProjectRoot root, string relativePath)
    {
        var source = File.ReadAllText(Path.Combine(root.FullPath, relativePath));
        return PackageDeclarationRegex().IsMatch(source);
    }

    [GeneratedRegex(@"\bpackage\s+(?!body\b)[A-Za-z_][A-Za-z0-9_$]*", RegexOptions.Compiled)]
    private static partial Regex PackageDeclarationRegex();
}
