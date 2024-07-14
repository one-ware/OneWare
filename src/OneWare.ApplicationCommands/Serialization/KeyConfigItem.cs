using Avalonia.Input;

namespace OneWare.ApplicationCommands.Serialization;

public class KeyConfigItem(string command, Key key, KeyModifiers keyModifiers)
{
    public string Command { get; init; } = command;

    public Key Key { get; } = key;

    public KeyModifiers KeyModifiers { get; } = keyModifiers;
}