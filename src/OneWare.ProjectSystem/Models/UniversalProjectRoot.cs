using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OneWare.Essentials.Converters;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.ProjectSystem.Models;

public abstract class UniversalProjectRoot : ProjectRoot, IProjectRootWithFile
{
    public DateTime LastSaveTime { get; set; }
    public override string ProjectPath => ProjectFilePath;
    public string ProjectFilePath { get; }
    public JsonObject Properties { get; }
    
    public UniversalProjectRoot(string projectFilePath, JsonObject properties) : base(Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"), false)
    {
        ProjectFilePath = projectFilePath;
        Properties = properties;

        Application.Current!.GetResourceObservable("UniversalProject").Subscribe(x => Icon = x as IImage);
    }

    public override bool IsPathIncluded(string relativePath)
    {
        var includes = GetProjectPropertyArray("Include");
        var excludes = GetProjectPropertyArray("Exclude");
        
        if (includes == null && excludes == null) return true;
        
        return ProjectHelper.MatchWildCards(relativePath, includes ?? new[] { "*.*" }, excludes);
    }

    public override void IncludePath(string path)
    {
        if(!Properties.ContainsKey("Include")) 
            Properties.Add("Include", new JsonArray());
        
        AddToProjectPropertyArray("Include", path);
    }
    
    public override void OnExternalEntryAdded(string path, FileAttributes attributes)
    {
        var relativePath = Path.GetRelativePath(FullPath, path);
        
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            var folder = AddFolder(relativePath);
            ProjectHelper.ImportEntries(path, folder);
            if(folder.Children.Count == 0) folder.TopFolder!.Remove(folder);
            return;
        }
        
        if (IsPathIncluded(relativePath)) AddFile(relativePath);
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
    
    public void SetProjectProperty(string name, string value)
    {
        Properties[name] = value;
    }
    
    public void SetProjectPropertyArray(string name, IEnumerable<string> values)
    {
        Properties[name] = new JsonArray(values.Select(x => JsonValue.Create(x)).ToArray());
    }

    protected void AddToProjectPropertyArray(string name, params string[] newItems)
    {
        Properties.TryAdd(name, new JsonArray());
        foreach (var item in newItems)
        {
            Properties[name]!.AsArray().Add(item);
        }
    }
    
    public void RemoveProjectProperty(string name)
    {
        Properties.Remove(name);
    }
}