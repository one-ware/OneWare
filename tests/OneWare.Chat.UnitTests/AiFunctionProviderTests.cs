using OneWare.Chat.Services;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Chat.UnitTests;

public class AiFunctionProviderTests
{
    [Fact]
    public void RegisterFunction_RaisesToolsChanged()
    {
        // The services are only needed to register the built-in functions, which this test doesn't trigger.
        var provider = new AiFunctionProvider(null!, null!, null!, null!, null!, null!, null!, null!);
        var raised = 0;
        provider.ToolsChanged += (_, _) => raised++;

        provider.RegisterFunction(new OneWareAiFunction
        {
            Name = "pluginTool",
            Description = "A tool registered by a plugin",
            Handler = () => "ok"
        });

        Assert.Equal(1, raised);
    }
}
