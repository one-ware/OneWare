﻿using System.Text.Json;
using System.Text.Json.Serialization;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Parser;

public static class FpgaSettingsParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true
    };

    private static string GetSettingPath(IProjectRoot project, string fpgaName)
    {
        return Path.Combine(project.FullPath, "device-settings", fpgaName + ".deviceconf");
    }

    public static void WriteDefaultSettingsIfEmpty(IProjectRoot project, IFpga fpga)
    {
        var path = GetSettingPath(project, fpga.Name);
        if (!File.Exists(path))
        {
            SaveSettings(project, fpga.Name, fpga.Properties);
        }
        else
        {
            var settings = LoadSettings(project, fpga.Name);
            foreach (var (key, value) in fpga.Properties) settings.TryAdd(key, value);
            SaveSettings(project, fpga.Name, settings);
        }
    }

    public static Dictionary<string, string> LoadSettings(IProjectRoot project, string fpgaName)
    {
        try
        {
            var path = GetSettingPath(project, fpgaName);
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(stream, SerializerOptions);

                return settings ?? [];
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }

        return [];
    }

    public static bool SaveSettings(IProjectRoot project, string fpgaName, IReadOnlyDictionary<string, string> settings)
    {
        try
        {
            var path = GetSettingPath(project, fpgaName);

            var folder = Path.GetDirectoryName(path);
            if (folder == null) throw new NullReferenceException(nameof(folder));
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var filteredSettings = settings
                .Where(x => string.IsNullOrEmpty(x.Value) == false)
                .ToDictionary(x => x.Key, x => x.Value);

            using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            JsonSerializer.Serialize(stream, filteredSettings, SerializerOptions);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }
}