namespace OneWare.Essentials.ToolEngine;

public class ToolContext(string name, string description, string key, List<string>? toolNames = null)
{
    public string Name { get; init; } = name;
    public string Description { get; init; } = description;
    public string Key { get; init; } = key;

    public List<string> ToolNames { get; init; } = toolNames ?? [];
}