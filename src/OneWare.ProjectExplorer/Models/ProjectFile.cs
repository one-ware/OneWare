using System.Globalization;
using System.Runtime.Serialization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using OneWare.Shared;
using OneWare.Shared.Converters;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Commands;
using Prism.Ioc;

namespace OneWare.ProjectExplorer.Models;

public class ProjectFile : ProjectEntry, IProjectFile
{
    public string Extension => Path.GetExtension(FullPath);
    
    public ProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
        var observable = SharedConverters.FileExtensionIconConverterObservable.Convert(Extension, typeof(IImage), null, CultureInfo.CurrentCulture) as IObservable<object?>;
        observable?.Subscribe(x =>
        {
            Icon = x as IImage;
        });
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