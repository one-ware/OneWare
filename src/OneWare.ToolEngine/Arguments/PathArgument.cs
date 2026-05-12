using System.Runtime.InteropServices;

namespace OneWare.Essentials.ToolEngine;

public class PathArgument(string path) : ICommandArgument
{
    private string _path = path;

    public void Prepare(OSPlatform osPlatform, Func<string, string>? pathMapper = null)
    {
        if (pathMapper != null) _path = pathMapper(_path);

        if (osPlatform == OSPlatform.Windows)
            _path = _path.Replace("/", "\\");
        else
            _path = _path.Replace("\\", "/");
    }

    public string GetArgument() => _path;
}