using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;

namespace OneWare.ProjectSystem.Models;

public class ProjectPropertyChangedEventArgs(string propertyName, object? oldValue, object? newValue) : EventArgs
{
    public string PropertyName { get; } = propertyName;
    
    public object? OldValue { get; } = oldValue;
    
    public object? NewValue { get; } =  newValue;
}

public abstract class UniversalProjectRoot : ProjectRoot, IProjectRootWithFile
{
    public UniversalProjectRoot(string projectFilePath, JsonObject properties) : base(
        Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"), false)
    {
        ProjectFilePath = projectFilePath;
        Properties = properties;

        Application.Current!.GetResourceObservable("UniversalProject").Subscribe(x => Icon = x as IImage);
    }

    public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;
    public JsonObject Properties { get; }
    public override string ProjectPath => ProjectFilePath;
    public DateTime LastSaveTime { get; set; }
    public string ProjectFilePath { get; }

    public override bool IsPathIncluded(string path)
    {
        return IsIncludedPathHelper(path, "Include", "Exclude");
    }

    public override void IncludePath(string path)
    {
        AddIncludedPathHelper(path, "Include");
    }

    protected bool IsIncludedPathHelper(string relativePath, string includeArrayKey, string? excludeArrayKey = null)
    {
        var includes = GetProjectPropertyArray(includeArrayKey);
        var excludes = excludeArrayKey == null ? null : GetProjectPropertyArray(excludeArrayKey);

        return ProjectHelper.MatchWildCards(relativePath, includes ?? ["*.*"], excludes);
    }
    
    protected void AddIncludedPathHelper(string relativePath, string includeArrayKey)
    {
        if (!Properties.ContainsKey(includeArrayKey))
            Properties.Add(includeArrayKey, new JsonArray());

        AddToProjectPropertyArray(includeArrayKey, relativePath);
    }
    
    protected void RemoveIncludedPathHelper(string relativePath, string includeArrayKey)
    {
        if (!Properties.ContainsKey(includeArrayKey))
            Properties.Add(includeArrayKey, new JsonArray());

        RemoveFromProjectPropertyArray(includeArrayKey, relativePath);
    }

    public string? GetProjectProperty(string name)
    {
        return Properties[name]?.ToString();
    }

    public IEnumerable<string>? GetProjectPropertyArray(string name)
    {
        return Properties[name]?.AsArray()
            .Where(x => x is not null)
            .Select(x => x!.ToString());
    }

    public void SetProjectProperty(string name, string? value)
    {
        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties[name] = value;
        ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(name, oldValue?.GetValue<object?>(), value));
    }

    public void SetProjectPropertyArray(string name, IEnumerable<string> values)
    {
        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties[name] = new JsonArray(values.Select(x => JsonValue.Create(x)).ToArray());
        ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(name, oldValue?.GetValue<object?>(), values));
    }

    public void AddToProjectPropertyArray(string name, params string[] newItems)
    {
        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties.TryAdd(name, new JsonArray());
        foreach (var item in newItems) Properties[name]!.AsArray().Add(item);
        ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(name, oldValue?.GetValue<object?>(), Properties[name]?.GetValue<object?>()));
    }

    public void RemoveFromProjectPropertyArray(string name, params string[] removeItems)
    {
        Properties.TryGetPropertyValue(name, out var oldValue);
        if (!Properties.ContainsKey(name)) return;
        foreach (var item in removeItems) Properties[name]!.AsArray().Remove(item);
        ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(name, oldValue?.GetValue<object?>(), Properties[name]?.GetValue<object?>()));
    }

    public void RemoveProjectProperty(string name)
    {
        Properties.TryGetPropertyValue(name, out var oldValue);
        Properties.Remove(name);
        ProjectPropertyChanged?.Invoke(this, new ProjectPropertyChangedEventArgs(name, oldValue?.GetValue<object?>(), null));
    }

    public override IProjectEntry? GetLoadedEntry(string relativePath)
    {
        if (relativePath.Equals(Path.GetFileName(ProjectFilePath), StringComparison.InvariantCultureIgnoreCase))
            return this;
        
        return base.GetLoadedEntry(relativePath);
    }
}
