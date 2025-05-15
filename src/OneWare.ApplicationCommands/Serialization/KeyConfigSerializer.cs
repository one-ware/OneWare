﻿using System.Text.Json;
using Avalonia.Input;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ApplicationCommands.Serialization;

public class KeyConfigSerializer
{
    private readonly ILogger _logger;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public KeyConfigSerializer(ILogger logger)
    {
        _logger = logger;
    }

    public void SaveHotkeys(string path, IList<IApplicationCommand> commands)
    {
        var ser = commands
            .Where(x => x.ActiveGesture != null && x.ActiveGesture != x.DefaultGesture)
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
            _logger.Error(e.Message, e);
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
                throw new Exception("Could not load Hotkey json");

            foreach (var hotkey in hotkeys)
            {
                var selected = commands.FirstOrDefault(x => x.Name == hotkey.Command);
                if (selected != null)
                    selected.ActiveGesture = new KeyGesture(hotkey.Key, hotkey.KeyModifiers);
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
