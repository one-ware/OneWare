using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.VcdViewer.Models;
using OneWare.VcdViewer.Parser;
using OneWare.WaveFormViewer.Controls;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.VcdViewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;

    private VcdFile? _vcdFile;
    public VcdFile? VcdFile
    {
        get => _vcdFile;
        set => SetProperty(ref _vcdFile, value);
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

    private async Task<bool> LoadAsync()
    {
        try
        {
            VcdFile = await VcdParser.ParseVcdAsync(FullPath);
            WaveFormViewer.Max = VcdFile.LastChangeTime;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            LoadingFailed = false;
        }
        
        IsLoading = false;
        return true;
    }

    public void AddSignal(VcdSignal signal)
    {
        WaveFormViewer.AddSignal(signal.Name, signal.Type, Construct(signal));
    }

    public WavePart[] Construct(VcdSignal signal)
    {
        if (signal.Changes.Any() && signal.Changes.Last().Time < VcdFile?.LastChangeTime)
        {
            signal.Changes.Add(new WavePart(VcdFile.LastChangeTime, signal.Changes.Last().Data));
        }
        return signal.Changes.ToArray();
    }
}