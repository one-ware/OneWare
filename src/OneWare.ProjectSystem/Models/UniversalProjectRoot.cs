using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public class ProjectPropertyChangedEventArgs(
    string propertyName,
    object? oldValue,
    object? newValue) : EventArgs
{
    public string PropertyName { get; } = propertyName;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
}

public abstract class UniversalProjectRoot : ProjectRoot, IProjectRootWithFile
{
    protected UniversalProjectRoot(string projectFilePath) : base(Path.GetDirectoryName(projectFilePath)
                                                                  ?? throw new NullReferenceException("Invalid Project Path"), false)
    {
        ProjectFilePath = projectFilePath;

        // Default icon
        Application.Current!
            .GetResourceObservable("UniversalProject")
            .Subscribe(x => Icon = x as IImage);
    }

    public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;

    protected void RaisePropertyChanged(
        string name,
        JsonNode? oldValue,
        object? newValue)
    {
        ProjectPropertyChanged?.Invoke(
            this,
            new ProjectPropertyChangedEventArgs(
                name,
                oldValue?.GetValue<object?>(),
                newValue
            )
        );
    }

    public UniversalProjectProperties Properties { get; private set; } = new();

    public override string ProjectPath => ProjectFilePath;

    public string ProjectFilePath { get; }

    public DateTime LastSaveTime { get; set; }
    
    public void LoadProperties(UniversalProjectProperties properties)
    {
        var normalized = NormalizeJsonKeys(properties);
        var oldProperties = Properties;
        Properties = normalized;
        RaisePropertyChanged("*", oldProperties, Properties);
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return key;

        return char.ToLowerInvariant(key[0]) + key[1..];
    }

    private static JsonObject NormalizeJsonKeys(JsonObject input)
    {
        var result = new JsonObject();

        foreach (var (key, value) in input)
        {
            var normalizedKey = NormalizeKey(key);

            if (value is JsonObject nestedObj)
                result[normalizedKey] = NormalizeJsonKeys(nestedObj);
            else
                result[normalizedKey] = value?.DeepClone();
        }

        return result;
    }
    

    public string? GetProjectProperty(string name)
    {
        name = NormalizeKey(name);
        return Properties[name]?.ToString();
    }

    public IEnumerable<string>? GetProjectPropertyArray(string name)
    {
        name = NormalizeKey(name);

        return Properties[name]?.AsArray()
            .Where(x => x is not null)
            .Select(x => x!.ToString());
    }

    public void SetProjectProperty(string name, string? value)
    {
        name = NormalizeKey(name);

        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties[name] = value;

        RaisePropertyChanged(name, oldValue, value);
    }

    public void RemoveProjectProperty(string name)
    {
        name = NormalizeKey(name);

        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties.Remove(name);

        RaisePropertyChanged(name, oldValue, null);
    }
    
    private JsonArray GetOrCreateArray(string name)
    {
        name = NormalizeKey(name);

        if (Properties[name] is not JsonArray arr)
        {
            arr = new JsonArray();
            Properties[name] = arr;
        }

        return arr;
    }

    public void SetProjectPropertyArray(string name, IEnumerable<string> values)
    {
        name = NormalizeKey(name);

        Properties.TryGetPropertyValue(name, out var oldValue);

        var array = new JsonArray(values.Select(x => JsonValue.Create(x)).ToArray());
        Properties[name] = array;

        RaisePropertyChanged(name, oldValue, values);
    }

    public void AddToProjectPropertyArray(string name, params string[] newItems)
    {
        var array = GetOrCreateArray(name);

        foreach (var item in newItems)
            array.Add(item);

        RaisePropertyChanged(name, null, array);
    }

    public void RemoveFromProjectPropertyArray(string name, params string[] removeItems)
    {
        name = NormalizeKey(name);

        if (Properties[name] is not JsonArray array)
            return;

        foreach (var item in removeItems)
            array.Remove(item);

        RaisePropertyChanged(name, null, array);
    }

    public override bool IsPathIncluded(string path)
    {
        return IsIncludedPathHelper(path, "include", "exclude");
    }

    public override void IncludePath(string path)
    {
        AddIncludedPathHelper(path, "include");
    }

    protected bool IsIncludedPathHelper(
        string relativePath,
        string includeArrayKey,
        string? excludeArrayKey = null)
    {
        var includes = GetProjectPropertyArray(includeArrayKey);
        var excludes = excludeArrayKey == null
            ? null
            : GetProjectPropertyArray(excludeArrayKey);

        if (includes is null)
            return false;

        return ProjectHelper.MatchWildCards(relativePath, includes, excludes);
    }

    protected void AddIncludedPathHelper(string relativePath, string includeArrayKey)
    {
        AddToProjectPropertyArray(includeArrayKey, relativePath);
    }

    protected void RemoveIncludedPathHelper(string relativePath, string includeArrayKey)
    {
        RemoveFromProjectPropertyArray(includeArrayKey, relativePath);
    }

    public override IProjectEntry? GetLoadedEntry(string relativePath)
    {
        if (relativePath.Equals(
                Path.GetFileName(ProjectFilePath),
                StringComparison.InvariantCultureIgnoreCase))
            return this;

        return base.GetLoadedEntry(relativePath);
    }
}
