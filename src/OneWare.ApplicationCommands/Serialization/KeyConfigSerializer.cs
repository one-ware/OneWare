using System.Text.Json;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ApplicationCommands.Serialization;

public static class KeyConfigSerializer
{
    // Separator used by WatchTreeChanges when building menu command paths
    private const string MenuPathSeparator = "\u2192";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void SaveHotkeys(string path, IList<IApplicationCommand> commands)
    {
        var ser = commands.Where(x => x.ActiveGesture != null && x.ActiveGesture != x.DefaultGesture)
            .Select(x => new KeyConfigItem(x.Name, x.ActiveGesture!.Key, x.ActiveGesture.KeyModifiers))
            .ToArray();

        try
        {
            using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            JsonSerializer.Serialize(stream, ser, Options);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    public static void LoadHotkeys(string path, IList<IApplicationCommand> commands)
    {
        if (!File.Exists(path)) return;

        try
        {
            using var stream = File.OpenRead(path);

            var hotkeys = JsonSerializer.Deserialize<KeyConfigItem[]>(stream, Options);

            if (hotkeys == null) throw new Exception("Could not load Hotkey json");

            foreach (var hotkey in hotkeys)
            {
                var selected = commands.FirstOrDefault(x => x.Name == hotkey.Command)
                               ?? FindLegacyCommand(commands, hotkeys, hotkey.Command);
                if (selected != null) selected.ActiveGesture = new KeyGesture(hotkey.Key, hotkey.KeyModifiers);
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }

    /// <summary>
    ///     Older versions saved menu commands that existed at startup by their header only
    ///     (e.g. "Find Files" instead of "View → Find Files"). Such an entry is applied to the
    ///     command whose name ends with it, as long as that match is unambiguous and the command
    ///     has no entry under its full name. The next save stores it under the full name.
    /// </summary>
    private static IApplicationCommand? FindLegacyCommand(IList<IApplicationCommand> commands,
        KeyConfigItem[] hotkeys, string savedName)
    {
        var suffix = $" {MenuPathSeparator} {savedName}";

        var candidates = commands
            .Where(x => x.Name.EndsWith(suffix, StringComparison.Ordinal))
            .Where(x => hotkeys.All(h => h.Command != x.Name))
            .Take(2)
            .ToArray();

        return candidates.Length == 1 ? candidates[0] : null;
    }
}