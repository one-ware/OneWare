using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.VcdViewer.Models;
using OneWare.VcdViewer.Parser;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.VcdViewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;

    private VcdDefinition? _vcdDefinition;
    public VcdDefinition? VcdDefinition
    {
        get => _vcdDefinition;
        set => SetProperty(ref _vcdDefinition, value);
    }

    private VcdScope? _selectedScope;
    public VcdScope? SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value);
    }

    private VcdSignal? _selectedSignal;
    public VcdSignal? SelectedSignal
    {
        get => _selectedSignal;
        set => SetProperty(ref _selectedSignal, value);
    }
    
    public WaveFormViewModel WaveFormViewer { get; } = new();

    public VcdViewModel(string fullPath, IProjectExplorerService projectExplorerService, IDockService dockService) : base(fullPath, projectExplorerService, dockService)
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
            VcdDefinition = VcdParser.ParseVcd(FullPath);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = false;
        }
        
        IsLoading = false;
        return Task.FromResult(true);
    }

    public void AddSignal(VcdSignal signal)
    {
        WaveFormViewer.Signals.Add(new WaveModel(signal.Name, Brushes.Aqua));
    }
}