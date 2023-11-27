using System.Text.Json.Nodes;
using Avalonia.Media.Imaging;
using OneWare.SDK.Converters;
using OneWare.SDK.Helpers;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
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
        
        Icon = SharedConverters.PathToBitmapConverter.Convert(ContainerLocator.Container.Resolve<IPaths>().AppIconPath, typeof(Bitmap), null, null) as Bitmap;
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
}