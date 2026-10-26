using System.Text.RegularExpressions;
using OneWare.Debugger.Models;

namespace OneWare.Debugger.Helpers;

// Sorts what GdbOutputFormatter passes on into the kinds the console colours and filters by.
public static partial class ConsoleLineClassifier
{
    public const string ErrorPrefix = "error: ";
    public const string TimeoutMessage = "error: GDB timed out";

    // afterUserCommand: the most recent command came from the user. Errors of the studio's own
    // commands (e.g. a hover on a word GDB does not know) are protocol, not something to report.
    public static ConsoleLineKind Classify(string text, bool afterUserCommand)
    {
        if (text == "(gdb)" || MiRecordRegex().IsMatch(text)) return ConsoleLineKind.Protocol;

        if (text == TimeoutMessage) return ConsoleLineKind.Error;

        if (text.StartsWith(ErrorPrefix, StringComparison.Ordinal))
            return afterUserCommand ? ConsoleLineKind.Error : ConsoleLineKind.Protocol;

        if (text.StartsWith("warning:", StringComparison.OrdinalIgnoreCase)) return ConsoleLineKind.Warning;

        return ConsoleLineKind.Output;
    }

    // Result (^done), exec (*stopped), status (+download) and notify (=thread-created) records,
    // optionally with a numeric token in front.
    [GeneratedRegex(@"^\d*[\^*+=][a-z][a-z-]*(,|$)")]
    private static partial Regex MiRecordRegex();
}
