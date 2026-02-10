using System;
using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Models;

public class ProjectPropertyChangedEventArgs(
    string propertyName,
    object? oldValue,
    object? newValue) : EventArgs
{
    public string PropertyName { get; } = propertyName;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
}

public class UniversalProjectProperties : IEnumerable<KeyValuePair<string, JsonNode?>>
{
    private JsonObject _data;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public UniversalProjectProperties()
        : this(new JsonObject())
    {
    }

    public UniversalProjectProperties(JsonObject? data)
    {
        _data = data ?? new JsonObject();
    }

    public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;

    public int Count => _data.Count;

    public JsonNode? this[string key]
    {
        get => GetNode(key);
        set => SetNode(key, value);
    }

    public JsonObject AsObject()
    {
        return _data;
    }

    public bool ContainsKey(string key)
    {
        return TryGetNodeByPath(key, out _);
    }

    public bool TryGetPropertyValue(string key, out JsonNode? value)
    {
        return TryGetNodeByPath(key, out value);
    }

    public bool Remove(string key)
    {
        return RemoveNodeInternal(key, out _);
    }

    public async Task<bool> LoadAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);

            var node = await JsonNode.ParseAsync(stream, null, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });

            if (node == null) return false;
            
            _data = node.AsObject();

            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }

    public async Task<bool> SaveAsync(string path)
    {
        try
        {
            await using var stream = File.OpenWrite(path);
            stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, _data, SerializerOptions);
            return true;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return false;
        }
    }

    public static UniversalProjectProperties FromJson(JsonObject data)
    {
        return new UniversalProjectProperties(data);
    }

    public string? GetString(string key)
    {
        return GetNode(key)?.ToString();
    }

    public JsonNode? GetNode(string key)
    {
        return GetNodeByPath(key);
    }

    public IEnumerable<string>? GetStringArray(string key)
    {
        if (GetNode(key) is not JsonArray array)
            yield break;

        foreach (var item in array)
        {
            if (item is not null)
                yield return item.ToString();
        }
    }

    public bool TryGetNode(string key, out JsonNode? value)
    {
        return TryGetNodeByPath(key, out value);
    }

    public void SetNode(string key, JsonNode? value)
    {
        SetNodeInternal(key, value);
    }

    public void SetString(string key, string? value)
    {
        SetNodeInternal(key, value == null ? null : JsonValue.Create(value));
    }

    public string RemoveValue(string key, out JsonNode? oldValue)
    {
        RemoveNodeInternal(key, out oldValue);
        return key;
    }

    public string AddToStringArray(string key, params string[] newItems)
    {
        var array = GetOrCreateArrayByPath(key, out var resolvedKey);
        foreach (var item in newItems)
            array.Add(item);

        RaisePropertyChanged(resolvedKey, null, array);
        return resolvedKey;
    }

    public string RemoveFromStringArray(string key, params string[] removeItems)
    {
        if (GetNode(key) is not JsonArray array)
            return key;

        foreach (var remove in removeItems)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (array[i]?.GetValue<string>() == remove)
                    array.RemoveAt(i);
            }
        }

        RaisePropertyChanged(key, null, array);
        return key;
    }

    private void RaisePropertyChanged(string name, object? oldValue, object? newValue)
    {
        ProjectPropertyChanged?.Invoke(
            this,
            new ProjectPropertyChangedEventArgs(name, oldValue, newValue)
        );
    }

    private JsonNode? SetNodeInternal(string key, JsonNode? value)
    {
        var oldValue = GetNodeByPath(key);
        SetNodeByPath(key, value);
        RaisePropertyChanged(key, oldValue, value);
        return oldValue;
    }

    private bool RemoveNodeInternal(string key, out JsonNode? oldValue)
    {
        if (!TryGetNodeByPath(key, out oldValue))
            return false;

        if (!RemoveNodeByPath(key))
            return false;

        RaisePropertyChanged(key, oldValue, null);
        return true;
    }

    private bool TryGetNodeByPath(string path, out JsonNode? value)
    {
        value = GetNodeByPath(path);
        return value is not null;
    }

    private JsonNode? GetNodeByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return null;

        JsonNode? current = _data;
        foreach (var segment in segments)
        {
            if (current is not JsonObject obj)
                return null;

            if (!TryGetNode(obj, segment, out var next))
                return null;

            current = next;
        }

        return current;
    }

    private void SetNodeByPath(string path, JsonNode? value)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return;

        var current = _data;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (TryGetNode(current, segment, out var next) && next is JsonObject obj)
            {
                current = obj;
                continue;
            }

            var resolvedSegment = ResolveKey(current, segment);
            var created = new JsonObject();
            current[resolvedSegment] = created;
            current = created;
        }

        var lastSegment = segments[^1];
        var resolvedKey = ResolveKey(current, lastSegment);
        current[resolvedKey] = value;
    }

    private bool RemoveNodeByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return false;

        var current = _data;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (!TryGetNode(current, segment, out var next) || next is not JsonObject obj)
                return false;

            current = obj;
        }

        if (!TryResolveKey(current, segments[^1], out var resolvedKey))
            return false;

        return current.Remove(resolvedKey);
    }

    private static bool TryGetNode(JsonObject obj, string key, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(key, out value))
            return true;

        if (TryResolveKey(obj, key, out var resolvedKey))
            return obj.TryGetPropertyValue(resolvedKey, out value);

        value = null;
        return false;
    }

    private static bool TryResolveKey(JsonObject obj, string key, out string resolvedKey)
    {
        foreach (var existingKey in obj)
        {
            if (string.Equals(existingKey.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = existingKey.Key;
                return true;
            }
        }

        resolvedKey = string.Empty;
        return false;
    }

    private static string ResolveKey(JsonObject obj, string key)
    {
        return TryResolveKey(obj, key, out var resolvedKey) ? resolvedKey : key;
    }

    private JsonArray GetOrCreateArrayByPath(string path, out string resolvedPath)
    {
        resolvedPath = path;
        if (string.IsNullOrWhiteSpace(path))
            return new JsonArray();

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return new JsonArray();

        var current = _data;
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (TryGetNode(current, segment, out var next) && next is JsonObject obj)
            {
                current = obj;
                continue;
            }

            var resolvedSegment = ResolveKey(current, segment);
            var created = new JsonObject();
            current[resolvedSegment] = created;
            current = created;
            segments[i] = resolvedSegment;
        }

        var lastSegment = segments[^1];
        var resolvedKey = ResolveKey(current, lastSegment);
        segments[^1] = resolvedKey;
        resolvedPath = string.Join('/', segments);

        if (current[resolvedKey] is not JsonArray array)
        {
            array = new JsonArray();
            current[resolvedKey] = array;
        }

        return array;
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
