using System.Runtime.InteropServices;

namespace OneWare.Essentials.ToolEngine;

public class TemplateArgument(string template, params (string placeholder, string path)[] pathMappings) : ICommandArgument
{
    private readonly Dictionary<string, string> _paths = pathMappings.ToDictionary(x => x.placeholder, x => x.path);
    private string _currentTemplate = template;

    public void Prepare(OSPlatform osPlatform, Func<string, string>? pathMapper = null)
    {
        var keys = _paths.Keys.ToList();
        foreach (var key in keys)
        {
            var p = _paths[key];
            if (pathMapper != null) p = pathMapper(p);
            p = osPlatform == OSPlatform.Windows ? p.Replace("/", "\\") : p.Replace("\\", "/");
            
            _paths[key] = p;
        }
    }

    public string GetArgument()
    {
        string result = _currentTemplate;
        foreach (var kvp in _paths)
        {
            result = result.Replace(kvp.Key, kvp.Value);
        }
        return result;
    }
}