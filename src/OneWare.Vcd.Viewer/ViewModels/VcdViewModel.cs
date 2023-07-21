using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.WaveFormViewer.Controls;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

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
        WaveFormViewer.ExtendSignals = true;
        
        _projectExplorerService = projectExplorerService;
        
        Title = $"Loading {Path.GetFileName(fullPath)}";
    }

    protected override void ChangeCurrentFile(IFile? oldFile)
    {
        _ = LoadAsync();
    }

    private async Task<bool> LoadAsync()
    {
        IsLoading = true;
        
        try
        {
            var progress = new Progress<int>();

            var context = VcdParser.ParseVcdDefinition(FullPath);

            VcdFile = context.Item1;
            
            progress.ProgressChanged += (o, i) =>
            {
                Title = $"{Path.GetFileName(FullPath)} {i}%";
                WaveFormViewer.Max = VcdFile.LastChangeTime;
            };

            await VcdParser.StartAndReportProgressAsync(context.Item2, context.Item1, progress);
            
            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Header}]" : CurrentFile!.Header;
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
        WaveFormViewer.AddSignal(signal.Name, signal.Type, signal.Changes);
    }

    protected override void Reset()
    {
        if (VcdFile == null) return;

        //Manual cleanup because ViewModel is stuck somewhere
        //TODO Fix this to allow normal GC collection
        foreach (var d in VcdFile.Definition.SignalRegister)
        {
            d.Value.Changes.Clear();
            d.Value.Changes.TrimExcess();
        }
    }
}