using System;
using System.IO;
using System.Text.Json;

namespace OneWare.Studio.Desktop;

internal static class EnvironmentDefaultsService
{
    private const string DefaultsFileName = "OneWareStudio.defaults.json";

    public static void Load()
    {
        LoadFromFile(GetDefaultsFilePath());
    }

    private static string GetDefaultsFilePath()
    {
        if (OperatingSystem.IsWindows())
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "OneWare",
                DefaultsFileName);

        if (OperatingSystem.IsMacOS())
            return Path.Combine("/Library/Application Support/OneWare", DefaultsFileName);

        if (OperatingSystem.IsLinux())
            return Path.Combine("/etc/oneware", DefaultsFileName);

        return Path.Combine(AppContext.BaseDirectory, DefaultsFileName);
    }

    private static void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                Warn(path, "the root value must be a JSON object");
                return;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!IsSupportedVariableName(property.Name))
                {
                    Warn(path, $"unsupported variable '{property.Name}' was ignored");
                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.String ||
                    property.Value.GetString() is not { } value ||
                    string.IsNullOrWhiteSpace(value) ||
                    value.Contains('\0'))
                {
                    Warn(path, $"invalid value for '{property.Name}' was ignored");
                    continue;
                }

                if (Environment.GetEnvironmentVariable(property.Name) is null)
                    Environment.SetEnvironmentVariable(property.Name, value);
            }
        }
        catch (JsonException exception)
        {
            Warn(path, $"invalid JSON was ignored: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            Warn(path, $"the file could not be read: {exception.Message}");
        }
        catch (IOException exception)
        {
            Warn(path, $"the file could not be read: {exception.Message}");
        }
    }

    private static bool IsSupportedVariableName(string name)
    {
        if (!name.StartsWith("ONEWARE_", StringComparison.Ordinal)) return false;

        foreach (var character in name)
            if (character != '_' &&
                (character < 'A' || character > 'Z') &&
                (character < '0' || character > '9'))
                return false;

        return true;
    }

    private static void Warn(string path, string message)
    {
        Console.Error.WriteLine($"Warning: Environment defaults file '{path}' {message}.");
    }
}
