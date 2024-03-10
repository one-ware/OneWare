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
    
    public UniversalProjectRoot(string projectFilePath, JsonObject properties) : base(Path.GetDirectoryName(projectFilePath) ?? throw new NullReferenceException("Invalid Project Path"))
    {
        ProjectFilePath = projectFilePath;
        Properties = properties;

        Application.Current!.GetResourceObservable("UniversalProject").Subscribe(x => Icon = x as IImage);
    }

    public override bool IsPathIncluded(string relativePath)
    {
        Properties.TryGetPropertyValue("Include", out var includeNode);
        var includes = includeNode?.AsArray().GetValues<string>();
        Properties.TryGetPropertyValue("Exclude", out var excludeNode);
        var excludes = excludeNode?.AsArray().GetValues<string>();
        
        if (includes == null && excludes == null) return true;
        
        return ProjectHelper.MatchWildCards(relativePath, includes ?? new[] { "*.*" }, excludes);
    }

    public override void IncludePath(string path)
    {
        if(!Properties.ContainsKey("Include")) 
            Properties.Add("Include", new JsonArray());
        
        Properties["Include"]!.AsArray().Add(path);
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
        
        if (!IsPathIncluded(relativePath)) return;
        
        AddFile(relativePath);
    }
}