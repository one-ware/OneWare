using System.Text.Json.Nodes;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.ProjectSystem;

namespace OneWare.ProjectSystem.Models;

public abstract class UniversalProjectRoot : ProjectRoot, IProjectRootWithFile
{
    private readonly List<Action<IProjectEntry>> _entryModificationHandlers = [];
    
    protected UniversalProjectRoot(string projectFilePath) : base(Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"))
    {
        ProjectFilePath = projectFilePath;

        Icon = new IconModel("UniversalProject");
    }

    public event EventHandler<ProjectPropertyChangedEventArgs>? ProjectPropertyChanged;

    protected void RaisePropertyChanged(
        string name,
        object? oldValue,
        object? newValue)
    {
        ProjectPropertyChanged?.Invoke(
            this,
            new ProjectPropertyChangedEventArgs(
                name,
                oldValue,
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
        var oldProperties = Properties;
        Properties = UniversalProjectProperties.FromJson(properties.AsObject());
        RaisePropertyChanged("*", oldProperties.AsObject(), Properties.AsObject());
    }


    public string? GetProjectProperty(string name)
    {
        return Properties.GetString(name);
    }

    public JsonNode? GetProjectPropertyNode(string name)
    {
        return Properties.GetNode(name);
    }

    public bool HasProjectProperty(string name)
    {
        return Properties.ContainsKey(name);
    }

    public IEnumerable<string>? GetProjectPropertyArray(string name)
    {
        return Properties.GetStringArray(name);
    }

    public void SetProjectProperty(string name, string? value)
    {
        var normalized = Properties.SetString(name, value, out var oldValue);
        RaisePropertyChanged(normalized, oldValue, value);
    }

    public void RemoveProjectProperty(string name)
    {
        var normalized = Properties.RemoveValue(name, out var oldValue);
        RaisePropertyChanged(normalized, oldValue, null);
    }

    public void SetProjectPropertyArray(string name, IEnumerable<string> values)
    {
        var normalized = Properties.SetStringArray(name, values, out var oldValue);
        RaisePropertyChanged(normalized, oldValue, values);
    }

    public void AddToProjectPropertyArray(string name, params string[] newItems)
    {
        var normalized = Properties.AddToStringArray(name, newItems);
        RaisePropertyChanged(normalized, null, Properties.GetNode(normalized));
    }

    public void RemoveFromProjectPropertyArray(string name, params string[] removeItems)
    {
        var normalized = Properties.RemoveFromStringArray(name, removeItems);
        RaisePropertyChanged(normalized, null, Properties.GetNode(normalized));
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
    
    public void RegisterEntryModification(Action<IProjectEntry> modificationAction)
    {
        _entryModificationHandlers.Add(modificationAction);
    }

    public void InvalidateModifications(IProjectEntry entry)
    {
        _entryModificationHandlers.ForEach(handler => handler(entry));
    }
}
