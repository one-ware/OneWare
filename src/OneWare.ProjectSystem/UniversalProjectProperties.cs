using System.Collections;
using System.Linq;
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
        get
        {
            key = NormalizeKey(key);
            return _data.TryGetPropertyValue(key, out var value) ? value : null;
        }
        set
        {
            key = NormalizeKey(key);
            _data[key] = value;
        }
    }

    public JsonObject AsObject()
    {
        return _data;
    }

    public bool ContainsKey(string key)
    {
        key = NormalizeKey(key);
        return _data.ContainsKey(key);
    }

    public bool TryGetPropertyValue(string key, out JsonNode? value)
    {
        key = NormalizeKey(key);
        return _data.TryGetPropertyValue(key, out value);
    }

    public bool Remove(string key)
    {
        key = NormalizeKey(key);
        return _data.Remove(key);
    }

    public static UniversalProjectProperties FromJson(JsonObject data)
    {
        return new UniversalProjectProperties(NormalizeJsonKeys(data));
    }

    public string? GetString(string key)
    {
        key = NormalizeKey(key);
        return _data.TryGetPropertyValue(key, out var value) ? value?.ToString() : null;
    }

    public JsonNode? GetNode(string key)
    {
        key = NormalizeKey(key);
        return _data.TryGetPropertyValue(key, out var value) ? value : null;
    }

    public IEnumerable<string>? GetStringArray(string key)
    {
        key = NormalizeKey(key);
        return _data.TryGetPropertyValue(key, out var value)
            ? value?.AsArray()
                .Where(x => x is not null)
                .Select(x => x!.ToString())
            : null;
    }

    public string SetString(string key, string? value, out JsonNode? oldValue)
    {
        key = NormalizeKey(key);
        _data.TryGetPropertyValue(key, out oldValue);
        _data[key] = value == null ? null : JsonValue.Create(value);
        return key;
    }

    public string RemoveValue(string key, out JsonNode? oldValue)
    {
        key = NormalizeKey(key);
        _data.TryGetPropertyValue(key, out oldValue);
        _data.Remove(key);
        return key;
    }

    public string SetStringArray(string key, IEnumerable<string> values, out JsonNode? oldValue)
    {
        key = NormalizeKey(key);
        _data.TryGetPropertyValue(key, out oldValue);
        var array = new JsonArray(values.Select(x => JsonValue.Create(x)).ToArray());
        _data[key] = array;
        return key;
    }

    public string AddToStringArray(string key, params string[] newItems)
    {
        var array = GetOrCreateArray(key, out var normalizedKey);
        foreach (var item in newItems)
            array.Add(item);
        return normalizedKey;
    }

    public string RemoveFromStringArray(string key, params string[] removeItems)
    {
        key = NormalizeKey(key);

        if (_data.TryGetPropertyValue(key, out var value) && value is JsonArray array)
        {
            foreach (var remove in removeItems)
            {
                for (int i = array.Count - 1; i >= 0; i--)
                {
                    if (array[i]?.GetValue<string>() == remove)
                    {
                        array.RemoveAt(i);
                    }
                }
            }
        }

        return key;
    }

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        var separators = new[] { '_', '-', ' ' };
        if (key.IndexOfAny(separators) < 0)
            return char.ToLowerInvariant(key[0]) + key[1..];

        var parts = key.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return char.ToLowerInvariant(key[0]) + key[1..];

        var normalized = parts
            .Select(part =>
            {
                if (string.IsNullOrEmpty(part)) return part;
                var lower = part.ToLowerInvariant();
                return char.ToUpperInvariant(lower[0]) + lower[1..];
            })
            .ToArray();

        normalized[0] = char.ToLowerInvariant(normalized[0][0]) + normalized[0][1..];
        return string.Concat(normalized);
    }

    private static JsonObject NormalizeJsonKeys(JsonObject input)
    {
        var result = new JsonObject();

        foreach (var (key, value) in input)
        {
            var normalizedKey = NormalizeKey(key);
            result[normalizedKey] = NormalizeJsonNode(value);
        }

        return result;
    }

    private static JsonNode? NormalizeJsonNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject nestedObj:
                return NormalizeJsonKeys(nestedObj);
            case JsonArray array:
                var normalizedArray = new JsonArray();
                foreach (var item in array)
                    normalizedArray.Add(NormalizeJsonNode(item));
                return normalizedArray;
            default:
                return node?.DeepClone();
        }
    }

    private JsonArray GetOrCreateArray(string key, out string normalizedKey)
    {
        normalizedKey = NormalizeKey(key);
        if (_data[normalizedKey] is not JsonArray arr)
        {
            arr = new JsonArray();
            _data[normalizedKey] = arr;
        }

        return arr;
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
