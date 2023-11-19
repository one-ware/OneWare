using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using OneWare.Shared.Controls;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : FlexibleWindowViewModelBase
{
    private readonly IWindowService _windowService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly UniversalFpgaProjectRoot _project;

    public ObservableCollection<FpgaModel> FpgaModels { get; } = new();

    private CompositeDisposable? _compositeDisposable;
    
    private FpgaModel? _selectedFpgaModel;
    public FpgaModel? SelectedFpgaModel
    {
        get => _selectedFpgaModel;
        set
        {
            SetProperty(ref _selectedFpgaModel, value);
            
            _compositeDisposable?.Dispose();
            _compositeDisposable = new CompositeDisposable();

            if (value is not null)
            {
                _project.Toolchain?.LoadConnections(_project, value);
                
                Observable.FromEventPattern(value, nameof(value.NodeConnected)).Subscribe(_ =>
                {
                    IsDirty = true;
                }).DisposeWith(_compositeDisposable);
                Observable.FromEventPattern(value, nameof(value.NodeDisconnected)).Subscribe(_ =>
                {
                    IsDirty = true;
                }).DisposeWith(_compositeDisposable);
            }
        }
    }

    private bool _hideExtensions;
    public bool HideExtensions
    {
        get => _hideExtensions;
        set => SetProperty(ref _hideExtensions, value);
    }
    
    public UniversalFpgaProjectCompileViewModel(IWindowService windowService, IProjectExplorerService projectExplorerService, FpgaService fpgaService, UniversalFpgaProjectRoot project)
    {
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
        _project = project;

        this.WhenValueChanged(x => x.IsDirty).Subscribe(x =>
        {
            Title = $"Connect and Compile - {_project.Header}{(x ? "*" : "")}";
        });
        
        //Construct FpgaModels
        foreach (var fpga in fpgaService.Fpgas)
        {
            fpgaService.CustomFpgaModels.TryGetValue(fpga, out var custom);
            custom ??= typeof(FpgaModel);
            var model = Activator.CreateInstance(custom, fpga) as FpgaModel;
            if (model is null) throw new NullReferenceException(nameof(model));
            FpgaModels.Add(model);
        }
        
        if (_project.TopEntity is IProjectFile file)
        {
            var provider = fpgaService.GetNodeProvider(file.Extension);
            if (provider is not null)
            {
                var nodes = provider.ExtractNodes(file).ToArray();
                foreach (var fpga in FpgaModels)
                {
                    foreach (var nodeModel in nodes)
                    {
                        fpga.AddNode(nodeModel);
                    }
                }
            }
        }
        
        SelectedFpgaModel = FpgaModels.FirstOrDefault();

        IsDirty = false;
    }

    public override void Close(FlexibleWindow window)
    {
        if(!IsDirty) window.Close();
        _ = SafeQuitAsync(window);
    }
    
    private async Task SafeQuitAsync(FlexibleWindow window)
    {
        var result = await _windowService.ShowYesNoCancelAsync("Warning", "Do you want to save changes?", MessageBoxIcon.Warning, window.Host);
        
        switch (result)
        {
            case MessageBoxStatus.Yes:
                SaveAndClose(window);
                return;
            case MessageBoxStatus.No:
                IsDirty = false;
                window.Close();
                return;
            case MessageBoxStatus.Canceled:
                return;
        }
    }
    
    public void SaveAndClose(FlexibleWindow window)
    {
        if (SelectedFpgaModel != null)
        {
            _project.Properties["Fpga"] = SelectedFpgaModel.Fpga.Name;
            _project.Toolchain?.SaveConnections(_project, SelectedFpgaModel);
            _ = _projectExplorerService.SaveProjectAsync(_project);
        }
        IsDirty = false;
        window.Close();
    }
}