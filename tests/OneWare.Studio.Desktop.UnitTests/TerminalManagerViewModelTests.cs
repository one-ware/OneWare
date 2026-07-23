using OneWare.TerminalManager.ViewModels;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class TerminalManagerViewModelTests
{
    private const string ExecutionMarkerPrefix = "\u001b]9;OW_DONE:execution:";

    [Fact]
    public void TryExtractCompletionMarker_RemovesPromptMarkedShellOutput()
    {
        var output = "20/20 seconds\r\nFinished after 20.0 seconds.\r\n";
        var current = output +
                      "\u001b]9;OW_DONE:0\u0007hmenn@aurora:/project$ \r\n" +
                      "\u001b[1A\r\u001b[2K" + ExecutionMarkerPrefix + "0\u0007" +
                      "\u001b]9;OW_DONE:0\u0007hmenn@aurora:/project$ ";

        var found = TerminalManagerViewModel.TryExtractCompletionMarker(
            current, ExecutionMarkerPrefix, out var exitCode, out var cleaned);

        Assert.True(found);
        Assert.Equal(0, exitCode);
        Assert.Equal(output, cleaned);
    }

    [Fact]
    public void TryExtractCompletionMarker_RemovesUnmarkedShellPrompts()
    {
        var output = "20/20 seconds\r\nFinished after 20.0 seconds.\r\n";
        var current = output +
                      "hmenn@aurora:/project$ \r\n" +
                      "\u001b[1A\r\u001b[2K" + ExecutionMarkerPrefix + "0\u0007" +
                      "hmenn@aurora:/project$ ";

        var found = TerminalManagerViewModel.TryExtractCompletionMarker(
            current, ExecutionMarkerPrefix, out var exitCode, out var cleaned);

        Assert.True(found);
        Assert.Equal(0, exitCode);
        Assert.Equal(output, cleaned);
    }
}
