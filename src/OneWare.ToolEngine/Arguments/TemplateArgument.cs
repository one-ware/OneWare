using System.Runtime.InteropServices;

namespace OneWare.Essentials.ToolEngine;

public class TemplateArgument : ICommandArgument
{
    private readonly string _template;
    private readonly List<(string placeholder, string value, bool isPath)> _mappings = new();
    private readonly Dictionary<string, string> _resolvedValues = new();

    public TemplateArgument(string template, params (string placeholder, string value, bool isPath)[] mappings)
    {
        _template = template;
        foreach (var m in mappings)
        {
            _mappings.Add(m);
            _resolvedValues[m.placeholder] = m.value;
        }
    }

    public void Prepare(OSPlatform osPlatform, Func<string, string>? pathMapper = null)
    {
        foreach (var (placeholder, value, isPath) in _mappings)
        {
            if (isPath)
            {
                var p = pathMapper != null ? pathMapper(value) : value;
                p = osPlatform == OSPlatform.Windows ? p.Replace("/", "\\") : p.Replace("\\", "/");
                _resolvedValues[placeholder] = p;
            }
            else
            {
                _resolvedValues[placeholder] = value;
            }
        }
    }

    public string GetArgument()
    {
        string result = _template;
        foreach (var kvp in _resolvedValues)
        {
            result = result.Replace(kvp.Key, kvp.Value);
        }
        return result;
    }
}