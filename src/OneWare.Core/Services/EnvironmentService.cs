using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class EnvironmentService : IEnvironmentService
{
    private static readonly string InitialPath = Environment.GetEnvironmentVariable("PATH")!;
    private readonly Dictionary<string, string> _paths = new();

    public void SetEnvironmentVariable(string key, string? value)
    {
        Environment.SetEnvironmentVariable(key, value);
    }

    public void SetPath(string key, string? path)
    {
        if (path != null)
            _paths[key] = path;
        else _paths.Remove(key);
        UpdateEnvironment();
    }

    public void RemovePath(string key)
    {
        _paths.Remove(key);
        UpdateEnvironment();
    }

    private void UpdateEnvironment()
    {
        //var currentPath = Environment.GetEnvironmentVariable("PATH");
        
        var pathSeparator = PlatformHelper.Platform switch
        {
            PlatformId.WinArm64 or PlatformId.WinX64 => ';',
            _ => ':'
        };
        
        var paths = string.Join(pathSeparator, _paths.Select(x => x.Value));
        
        Environment.SetEnvironmentVariable("PATH", $"{paths}{pathSeparator}{InitialPath}");
    }
}