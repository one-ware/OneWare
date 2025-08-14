using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ToolService : IToolService
{
    private readonly List<ToolDescription> _tools = new();
    
    public void Register(ToolDescription description)
    {
        _tools.Add(description);
    }

    public void Unregister(ToolDescription description)
    {
        _tools.Remove(description);
    }

    public void Unregister(string toolKey)
    {
        var tool = _tools.FirstOrDefault(t => t.ToolKey == toolKey);
        
        if (tool is null)
        {
            throw new InvalidOperationException($"Tool with key '{toolKey}' not found.");
        }
        
        Unregister(tool);
    }
}