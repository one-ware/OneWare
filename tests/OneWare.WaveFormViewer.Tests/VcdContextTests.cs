using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Context;
using OneWare.WaveFormViewer.Enums;
using Xunit;

namespace OneWare.WaveFormViewer.Tests;

public class VcdContextTests
{
    private static Dictionary<string, IVcdSignal> CreateRegister(params (string id, string name)[] signals)
    {
        return signals.ToDictionary(x => x.id,
            x => (IVcdSignal)new VcdSignal<StdLogic>([], VcdLineType.Wire, 1, x.id, x.name));
    }

    private static VcdContextSignal ContextSignal(string id, string? name)
    {
        return new VcdContextSignal(id, WaveDataType.Binary, false, 0) { Name = name };
    }

    [Fact]
    public void ResolveSignals_MatchesByIdOrUniqueName()
    {
        var register = CreateRegister(("!", "clk"), ("#", "rst"), ("$", "data"), ("%", "data"));
        var context = new VcdContext([
            ContextSignal("!", "clk"), // same id and name
            ContextSignal("?", "rst"), // unknown id, unique name
            ContextSignal("!", "rst"), // id belongs to another signal
            ContextSignal("#", null), // old profile without name
            ContextSignal("?", "data"), // ambiguous name
            ContextSignal("?", "missing")
        ]);

        var result = VcdContextManager.ResolveSignals(context, register).Select(x => x.Signal.Id).ToArray();

        Assert.Equal(["!", "#", "#", "#"], result);
    }

    [Fact]
    public async Task SaveAndLoad_KeepsSeparatorAndName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vcdconf");
        try
        {
            var context = new VcdContext([
                new VcdContextSignal("!", WaveDataType.Hex, true, 3) { Name = "data", SeparatorLabel = "Bus" }
            ]);

            Assert.True(await VcdContextManager.SaveContextAsync(path, context));
            var loaded = (await VcdContextManager.LoadContextAsync(path))!.OpenSignals!.Single();

            Assert.Equal("!", loaded.Id);
            Assert.Equal("data", loaded.Name);
            Assert.Equal("Bus", loaded.SeparatorLabel);
            Assert.Equal(WaveDataType.Hex, loaded.DataType);
            Assert.True(loaded.AutomaticFixedPointShift);
            Assert.Equal(3, loaded.FixedPointShift);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Load_OldFormatWithoutNameAndSeparator()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.vcdconf");
        try
        {
            await File.WriteAllTextAsync(path, """
                                               {
                                                 "OpenSignals": [
                                                   { "Id": "!", "DataType": 3, "AutomaticFixedPointShift": false, "FixedPointShift": 0 }
                                                 ]
                                               }
                                               """);

            var loaded = (await VcdContextManager.LoadContextAsync(path))!.OpenSignals!.Single();

            Assert.Equal("!", loaded.Id);
            Assert.Null(loaded.Name);
            Assert.Null(loaded.SeparatorLabel);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
