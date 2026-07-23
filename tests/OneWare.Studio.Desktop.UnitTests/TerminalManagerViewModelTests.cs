using OneWare.TerminalManager.ViewModels;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class TerminalManagerViewModelTests
{
    [Fact]
    public void TrimCompletedOutput_RemovesPromptAndControlSequence()
    {
        var output = "20/20 seconds\r\nFinished after 20.0 seconds.\r\n";
        var current = output +
                      "hmenn@aurora:/project$ \r\n" +
                      "\u001b[1A\r\u001b[2K";

        var cleaned = TerminalManagerViewModel.TrimCompletedOutput(current);

        Assert.Equal(output, cleaned);
    }

    [Fact]
    public void TrimCompletedOutput_PreservesOutputWithoutControlSequence()
    {
        const string output = "Finished without framing";

        Assert.Equal(output, TerminalManagerViewModel.TrimCompletedOutput(output));
    }
}
