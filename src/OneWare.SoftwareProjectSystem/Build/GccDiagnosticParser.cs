using System.Text.RegularExpressions;
using OneWare.Essentials.Enums;

namespace OneWare.SoftwareProjectSystem.Build;

public sealed record CompilerDiagnostic(
    string? FilePath,
    int Line,
    int? Column,
    ErrorType Type,
    string Message);

/// <summary>
/// Parses GCC / Clang diagnostics like <c>src/main.cpp:5:3: error: message</c>.
/// </summary>
public static partial class GccDiagnosticParser
{
    [GeneratedRegex(
        @"^(?<file>(?:[A-Za-z]:)?[^:\r\n]+):(?<line>\d+)(?::(?<col>\d+))?:\s*(?<sev>fatal error|error|warning|note):\s*(?<msg>.*)$")]
    private static partial Regex LocatedDiagnostic();

    [GeneratedRegex(@"(undefined reference to|multiple definition of|ld returned \d+ exit status|ld: error:|cannot find -l)")]
    private static partial Regex LinkerError();

    public static bool TryParse(string line, string workingDirectory, out CompilerDiagnostic diagnostic)
    {
        var match = LocatedDiagnostic().Match(line.TrimEnd());
        if (match.Success)
        {
            var file = match.Groups["file"].Value.Trim();
            if (!Path.IsPathRooted(file)) file = Path.GetFullPath(Path.Combine(workingDirectory, file));

            diagnostic = new CompilerDiagnostic(
                file,
                int.Parse(match.Groups["line"].Value),
                match.Groups["col"].Success ? int.Parse(match.Groups["col"].Value) : null,
                match.Groups["sev"].Value switch
                {
                    "warning" => ErrorType.Warning,
                    "note" => ErrorType.Hint,
                    _ => ErrorType.Error
                },
                match.Groups["msg"].Value.Trim());
            return true;
        }

        if (LinkerError().IsMatch(line))
        {
            diagnostic = new CompilerDiagnostic(null, 0, null, ErrorType.Error, line.Trim());
            return true;
        }

        diagnostic = null!;
        return false;
    }
}
