using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData;
using OneWare.Shared;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.Views;
using OneWare.Vcd.Parser;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Viewer.Models;
using OneWare.WaveFormViewer.Controls;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;
using OneWare.WaveFormViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Vcd.Viewer.ViewModels;

public class VcdViewModel : ExtendedDocument
{
    private readonly IProjectExplorerService _projectExplorerService;

    private VcdFile? _vcdFile;
    public ObservableCollection<VcdScopeModel> Scopes { get; } = new();
    
    private VcdScopeModel? _selectedScope;
    public VcdScopeModel? SelectedScope
    {
        get => _selectedScope;
        set => SetProperty(ref _selectedScope, value);
    }

    private IVcdSignal? _selectedSignal;
    public IVcdSignal? SelectedSignal
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
        Scopes.Clear();
        
        try
        {
            var progress = new Progress<int>();

            var context = VcdParser.ParseVcdDefinition(FullPath);

            _vcdFile = context.Item1;
            
            Scopes.AddRange(_vcdFile.Definition.Scopes.Select(x => new VcdScopeModel(x)));
            
            progress.ProgressChanged += (o, i) =>
            {
                Title = $"{Path.GetFileName(FullPath)} {i}%";
                WaveFormViewer.Max = context.Item1.Definition.ChangeTimes.Last();
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

    public void AddSignal(IVcdSignal signal)
    {
        WaveFormViewer.AddSignal(signal);
    }

    protected override void Reset()
    {
        if (_vcdFile == null) return;

        //Manual cleanup because ViewModel is stuck somewhere
        //TODO Fix DOCK to allow normal GC collection
        foreach (var (_, signal) in _vcdFile.Definition.SignalRegister)
        {
            signal.Clear();
        }
        _vcdFile.Definition.ChangeTimes.Clear();
        _vcdFile.Definition.ChangeTimes.TrimExcess();
    }
}