using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using OneWare.Shared;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Commands;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Models;

[DataContract]
public class ProjectFile : ProjectEntry, IProjectFile
{
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        
    }

    public int Version { get; set; }
    public override IEnumerable<MenuItemViewModel> ContextMenu
    {
        get
        {
            yield return new MenuItemViewModel()
            {
                Header = "Open",
                Command = new RelayCommand(() => ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(this))
            };
        }
    }
}