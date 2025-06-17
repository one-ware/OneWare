using System.Text.Json;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ApplicationCommands.Serialization;

public class KeyConfigSerializer
{
    private readonly ILogger<KeyConfigSerializer> _logger;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public KeyConfigSerializer(ILogger<KeyConfigSerializer> logger)
    {
        _logger = logger;
    }

    public void SaveHotkeys(string path, IList<IApplicationCommand> commands)
    {
        var ser = commands
            .Where(x => x.ActiveGesture != null && !x.ActiveGesture.Equals(x.DefaultGesture))
            .Select(x => new KeyConfigItem(x.Name, x.ActiveGesture!.Key, x.ActiveGesture.KeyModifiers))
            .ToArray();

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var stream = File.Create(path);
            JsonSerializer.Serialize(stream, ser, Options);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to save hotkeys to {Path}", path);
        }
    }

    public void LoadHotkeys(string path, IList<IApplicationCommand> commands)
    {
        if (!File.Exists(path)) return;

        try
        {
            using var stream = File.OpenRead(path);
            var hotkeys = JsonSerializer.Deserialize<KeyConfigItem[]>(stream, Options);

            if (hotkeys == null)
            {
                _logger.LogWarning("Hotkey file {Path} was empty or invalid", path);
                return;
            }

            foreach (var hotkey in hotkeys)
            {
                var command = commands.FirstOrDefault(x => x.Name == hotkey.Command);
                if (command != null)
                {
                    command.ActiveGesture = new KeyGesture(hotkey.Key, hotkey.KeyModifiers);
                }
                else
                {
                    _logger.LogWarning("Command {Command} not found in available commands", hotkey.Command);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load hotkeys from {Path}", path);
        }
    }
}