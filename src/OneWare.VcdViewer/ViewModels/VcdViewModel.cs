using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.VcdViewer.Parser;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.VcdViewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;
    public WaveFormViewModel WaveFormViewer { get; } = new();

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService) : base(fullPath, projectExplorerService)
    {
        _projectExplorerService = projectExplorerService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";
    }

    protected override void ChangeCurrentFile(IFile? oldFile)
    {
        _ = LoadAsync();
    }

    private Task<bool> LoadAsync()
    {
        try
        {
            var definition = VcdParser.ParseVcd(FullPath);
            foreach (var scope in definition.Scopes)
            {
                foreach (var signal in scope.Signals)
                {
                    WaveFormViewer.Signals.Add(new WaveModel(signal.Name, Brushes.Aqua));
                }
            }
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = false;
        }
        
        IsLoading = false;
        return Task.FromResult(true);
    }
}