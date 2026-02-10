using System.Text.Json.Nodes;

namespace OneWare.UniversalFpgaProjectSystem.Context;

public class TestBenchContext(string filePath, JsonObject properties)
{
    public string FilePath { get; } = filePath;

    public string? Simulator
    {
        get => Properties["simulator"]?.ToString();
        set => Properties["simulator"] = value;
    }

    public JsonObject Properties { get; } = properties;

    public string? GetBenchProperty(string name)
    {
        return Properties[name]?.ToString();
    }

    public IEnumerable<string>? GetBenchPropertyArray(string name)
    {
        return Properties[name]?.AsArray()
            .Where(x => x is not null)
            .Select(x => x!.ToString());
    }

    public void SetBenchProperty(string name, string value)
    {
        Properties[name] = value;
    }

    public void SetBenchPropertyArray(string name, IEnumerable<string> values)
    {
        Properties[name] = new JsonArray(values.Select(x => JsonValue.Create(x)).ToArray());
    }

    protected void AddToBenchPropertyArray(string name, params string[] newItems)
    {
        Properties.TryAdd(name, new JsonArray());
        foreach (var item in newItems) Properties[name]!.AsArray().Add(item);
    }

    public void RemoveBenchProperty(string name)
    {
        Properties.Remove(name);
    }
}
