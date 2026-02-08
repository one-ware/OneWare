using System.Reactive.Disposables;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.ProjectSystem.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        IconModel = new IconModel()
        {
            IconObservable = ContainerLocator.Container.Resolve<IFileIconService>().GetFileIcon(Extension)
        };
    }

    public string Extension => Path.GetExtension(FullPath);
}
