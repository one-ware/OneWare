using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectPinPlannerViewModel : FlexibleWindowViewModelBase
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;
    private readonly FpgaService _fpgaService;

    private CompositeDisposable? _compositeDisposable;

    private bool _hideExtensions;

    private IFpgaPackage? _selectedPackage;

    private FpgaModel? _selectedModel;

    private FpgaViewModelBase? _selectedViewModel;

    private readonly FpgaNode[]? _nodes;

    public UniversalFpgaProjectPinPlannerViewModel(IWindowService windowService,
        IProjectExplorerService projectExplorerService, FpgaService fpgaService, UniversalFpgaProjectRoot project)
    {
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;
        Project = project;

        TopRightExtension = windowService.GetUiExtensions("CompileWindow_TopRightExtension");

        this.WhenValueChanged(x => x.IsDirty).Subscribe(x =>
        {
            Title = $"Pin Planner - {Project.Header}{(x ? "*" : "")}";
        });

        if (Project.TopEntity is IProjectFile file)
        {
            var provider = fpgaService.GetNodeProvider(file.Extension);
            if (provider is not null)
            {
                try
                {
                    _nodes = provider.ExtractNodes(file).ToArray();
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
                }
            }
        }

        RefreshHardware();

        SelectedFpgaPackage = FpgaPackages.FirstOrDefault(x => x.Name == project.GetProjectProperty("Fpga")) ??
                              FpgaPackages.FirstOrDefault();

        IsDirty = false;
    }

    public static KeyGesture SaveGesture => new(Key.S, PlatformHelper.ControlKey);

    public ObservableCollection<UiExtension> TopRightExtension { get; }

    public UniversalFpgaProjectRoot Project { get; }

    public ObservableCollection<IFpgaPackage> FpgaPackages { get; } = new();

    public IFpgaPackage? SelectedFpgaPackage
    {
        get => _selectedPackage;
        set
        {
            if (_selectedPackage?.Name != value?.Name) IsDirty = true;

            SetProperty(ref _selectedPackage, value);

            if (value != null)
            {
                SelectedFpgaModel = new FpgaModel(value.LoadFpga());
                SelectedFpgaViewModel = value.LoadFpgaViewModel(SelectedFpgaModel);
            }
            else
            {
                SelectedFpgaModel = null;
                SelectedFpgaViewModel = null;
            }
        }
    }

    public FpgaModel? SelectedFpgaModel
    {
        get => _selectedModel;
        private set
        {
            SetProperty(ref _selectedModel, value);

            _compositeDisposable?.Dispose();
            _compositeDisposable = new CompositeDisposable();

            if (value is not null)
            {
                if (_nodes != null)
                {
                    foreach (var nodeModel in _nodes)
                        value.AddNode(nodeModel);
                }

                Project.Toolchain?.LoadConnections(Project, value);

                Observable.FromEventPattern(value, nameof(value.NodeConnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
                Observable.FromEventPattern(value, nameof(value.NodeDisconnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
            }
        }
    }

    public FpgaViewModelBase? SelectedFpgaViewModel
    {
        get => _selectedViewModel;
        private set { SetProperty(ref _selectedViewModel, value); }
    }

    public bool HideExtensions
    {
        get => _hideExtensions;
        set => SetProperty(ref _hideExtensions, value);
    }

    public async Task RefreshFpgasButtonAsync(FlexibleWindow window)
    {
        if (IsDirty)
        {
            var result = await _windowService.ShowYesNoCancelAsync("Warning", "Do you want to save changes?",
                MessageBoxIcon.Warning, window.Host);

            switch (result)
            {
                case MessageBoxStatus.Yes:
                    Save();
                    break;
                case MessageBoxStatus.No:
                    IsDirty = false;
                    break;
                case MessageBoxStatus.Canceled:
                    return;
            }
        }

        _fpgaService.LoadGenericHardware();
        RefreshHardware();
    }

    private void RefreshHardware()
    {
        var oldSelectedFpgaPackageName = SelectedFpgaPackage?.Name;

        FpgaPackages.Clear();

        SelectedFpgaPackage = null;

        //Construct FpgaModels
        foreach (var package in _fpgaService.FpgaPackages)
        {
            FpgaPackages.Add(package);
        }

        if (oldSelectedFpgaPackageName != null)
        {
            SelectedFpgaPackage = FpgaPackages.FirstOrDefault(x => x.Name == oldSelectedFpgaPackageName);
        }

        IsDirty = false;
    }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        if (!IsDirty)
        {
            SelectedFpgaPackage = null;
            return true;
        }
        _ = SafeQuitAsync(window);
        return false;
    }

    private async Task SafeQuitAsync(FlexibleWindow window)
    {
        var result = await _windowService.ShowYesNoCancelAsync("Warning", "Do you want to save changes?",
            MessageBoxIcon.Warning, window.Host);

        switch (result)
        {
            case MessageBoxStatus.Yes:
                SaveAndClose(window);
                return;
            case MessageBoxStatus.No:
                IsDirty = false;
                Close(window);
                return;
            case MessageBoxStatus.Canceled:
                return;
        }
    }

    public void Save()
    {
        if (SelectedFpgaModel != null)
        {
            FpgaSettingsParser.WriteDefaultSettingsIfEmpty(Project, SelectedFpgaModel.Fpga);
            Project.SetProjectProperty("Fpga", SelectedFpgaModel.Fpga.Name);
            Project.Toolchain?.SaveConnections(Project, SelectedFpgaModel);
            _ = _projectExplorerService.SaveProjectAsync(Project);

            IsDirty = false;
        }
    }

    public void SaveAndClose(FlexibleWindow window)
    {
        Save();
        window.Close();
    }

    public void SaveAndCompile(FlexibleWindow window)
    {
        SaveAndClose(window);
        if (SelectedFpgaModel != null) _ = Project.RunToolchainAsync(SelectedFpgaModel);
    }
}