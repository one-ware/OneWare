using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DynamicData.Binding;
using OneWare.ProjectSystem.Models;

namespace OneWare.FolderProjectSystem.Models;

public class FolderProjectRoot : ProjectRoot
{
    public const string ProjectType = "Folder";
    public override string ProjectPath => RootFolderPath;
    public override string ProjectTypeId => ProjectType;

    public FolderProjectRoot(string rootFolderPath) : base(rootFolderPath)
    {
        IDisposable? iconDisposable = null;
        this.WhenValueChanged(x => x.IsExpanded).Subscribe(x =>
        {
            iconDisposable?.Dispose();
            if (!x)
            {
                iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.Folder16X").Subscribe(y =>
                {
                    Icon = y as IImage;
                });
            }
            else
            {
                iconDisposable = Application.Current?.GetResourceObservable("VsImageLib.FolderOpen16X").Subscribe(y =>
                {
                    Icon = y as IImage;
                });
            }
        });
    }

}