using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;

namespace OneWare.FolderProjectSystem.Models;

public class FolderProjectRoot : ProjectRoot
{
    public const string ProjectType = "Folder";

    public FolderProjectRoot(string rootFolderPath) : base(rootFolderPath)
    {
        
    }

    public override string ProjectPath => RootFolderPath;
    
    public override string ProjectTypeId => ProjectType;

    public override bool IsPathIncluded(string path)
    {
        return true;
    }

    public override void IncludePath(string path)
    {
        //Not needed
    }
}