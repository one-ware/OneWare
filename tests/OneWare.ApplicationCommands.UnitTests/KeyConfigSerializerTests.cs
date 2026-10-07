using System.Text.Json;
using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.Serialization;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.ApplicationCommands.UnitTests;

public class KeyConfigSerializerTests : IDisposable
{
    private static readonly KeyGesture SavedGesture = new(Key.F, KeyModifiers.Alt | KeyModifiers.Shift);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"keyConfig-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public void LoadHotkeys_AppliesExactName()
    {
        var command = new TestCommand("View \u2192 Find Files");
        WriteConfig("View \u2192 Find Files");

        KeyConfigSerializer.LoadHotkeys(_path, [command]);

        Assert.Equal(SavedGesture, command.ActiveGesture);
    }

    [Fact]
    public void LoadHotkeys_AppliesLegacyNameWithoutMenuPath()
    {
        var command = new TestCommand("View \u2192 Find Files");
        WriteConfig("Find Files");

        KeyConfigSerializer.LoadHotkeys(_path, [command]);

        Assert.Equal(SavedGesture, command.ActiveGesture);
    }

    [Fact]
    public void LoadHotkeys_IgnoresAmbiguousLegacyName()
    {
        var first = new TestCommand("File \u2192 Save");
        var second = new TestCommand("Edit \u2192 Save");
        WriteConfig("Save");

        KeyConfigSerializer.LoadHotkeys(_path, [first, second]);

        Assert.Null(first.ActiveGesture);
        Assert.Null(second.ActiveGesture);
    }

    [Fact]
    public void LoadHotkeys_PrefersEntryWithFullName()
    {
        var command = new TestCommand("View \u2192 Find Files");
        var fullNameGesture = new KeyGesture(Key.G, KeyModifiers.Control);
        File.WriteAllText(_path, JsonSerializer.Serialize(new[]
        {
            new KeyConfigItem("Find Files", SavedGesture.Key, SavedGesture.KeyModifiers),
            new KeyConfigItem("View \u2192 Find Files", fullNameGesture.Key, fullNameGesture.KeyModifiers)
        }));

        KeyConfigSerializer.LoadHotkeys(_path, [command]);

        Assert.Equal(fullNameGesture, command.ActiveGesture);
    }

    [Fact]
    public void LoadHotkeys_DoesNotMatchPartialHeader()
    {
        var command = new TestCommand("View \u2192 Find Files");
        WriteConfig("Files");

        KeyConfigSerializer.LoadHotkeys(_path, [command]);

        Assert.Null(command.ActiveGesture);
    }

    private void WriteConfig(string commandName)
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(new[]
        {
            new KeyConfigItem(commandName, SavedGesture.Key, SavedGesture.KeyModifiers)
        }));
    }

    private sealed class TestCommand(string name) : ApplicationCommandBase(name)
    {
        public override bool Execute(ILogical source) => false;

        public override bool CanExecute(ILogical source) => false;
    }
}
