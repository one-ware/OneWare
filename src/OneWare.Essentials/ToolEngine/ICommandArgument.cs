using System.Runtime.InteropServices;

namespace OneWare.Essentials.ToolEngine;

public interface ICommandArgument
{
    string GetArgument();

    void Prepare(OSPlatform osPlatform, Func<string, string>? pathMapper = null);
}