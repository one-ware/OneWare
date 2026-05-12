using System.Runtime.InteropServices;

namespace OneWare.Essentials.ToolEngine;

public class CommandArgument(string argument) : ICommandArgument
{
    public void Prepare(OSPlatform osPlatform, Func<string, string>? pathMapper = null) {}
    public string GetArgument() => argument;
}