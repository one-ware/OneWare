using System.Collections;
using System.Text.Json.Nodes;

namespace OneWare.ProjectSystem;

public class UniversalProjectProperties : IEnumerable<KeyValuePair<string, JsonNode?>>
{
    private readonly JsonObject _data;

    public UniversalProjectProperties()
        : this(new JsonObject())
    {
    }

    public UniversalProjectProperties(JsonObject data)
    {
        _data = data ?? new JsonObject();
    }

    public int Count => _data.Count;

    public JsonNode? this[string key]
    {
        get => _data.TryGetPropertyValue(key, out var value) ? value : null;
        set => _data[key] = value;
    }

    public JsonObject AsObject()
    {
        return _data;
    }

    public bool ContainsKey(string key)
    {
        return _data.ContainsKey(key);
    }

    public bool TryGetPropertyValue(string key, out JsonNode? value)
    {
        return _data.TryGetPropertyValue(key, out value);
    }

    public bool Remove(string key)
    {
        return _data.Remove(key);
    }

    public IEnumerator<KeyValuePair<string, JsonNode?>> GetEnumerator()
    {
        return _data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
