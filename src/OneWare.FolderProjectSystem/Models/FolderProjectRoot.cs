using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.ProjectSystem.Models;

namespace OneWare.FolderProjectSystem.Models;

public class FolderProjectRoot : ProjectRoot
{
    public const string ProjectType = "Folder";

    public FolderProjectRoot(string rootFolderPath) : base(rootFolderPath, true)
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

    public override void OnExternalEntryAdded(string relativePath, FileAttributes attributes)
    {
        var parentPath = Path.GetDirectoryName(relativePath);
        if (parentPath != null && GetLoadedEntry(parentPath) is IProjectFolder folder)
        {
            if (folder.IsExpanded)
            {
                if (attributes.HasFlag(FileAttributes.Directory))
                    AddFolder(relativePath);
                else
                    AddFile(relativePath);
            }
            else if (folder.Children.Count == 0)
            {
                folder.Children.Add(new LoadingDummyNode());
            }
        }
    }
}