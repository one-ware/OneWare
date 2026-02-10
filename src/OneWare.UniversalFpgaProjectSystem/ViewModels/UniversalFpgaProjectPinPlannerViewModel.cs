using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
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

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectPinPlannerViewModel : FlexibleWindowViewModelBase
{
    private readonly FpgaService _fpgaService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    private CompositeDisposable? _compositeDisposable;

    private FpgaNode[]? _nodes;

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

        Toolchain = _fpgaService.Toolchains.FirstOrDefault(x => x.Id == Project.Toolchain);
        _ = InitializeAsync();
    }

    public IFpgaToolchain? Toolchain { get; }

    public bool IsLoading
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public static KeyGesture SaveGesture => new(Key.S, PlatformHelper.ControlKey);

    public ObservableCollection<OneWareUiExtension> TopRightExtension { get; }

    public UniversalFpgaProjectRoot Project { get; }

    public ObservableCollection<IFpgaPackage> FpgaPackages { get; } = new();

    public IFpgaPackage? SelectedFpgaPackage
    {
        get;
        set
        {
            if (field?.Name != value?.Name) IsDirty = true;

            SetProperty(ref field, value);

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
        get;
        private set
        {
            SetProperty(ref field, value);

            _compositeDisposable?.Dispose();
            _compositeDisposable = new CompositeDisposable();

            if (value is not null)
            {
                if (_nodes != null)
                    foreach (var nodeModel in _nodes)
                        value.AddNode(nodeModel);

                Toolchain?.LoadConnections(Project, value);

                Observable.FromEventPattern(value, nameof(value.NodeConnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
                Observable.FromEventPattern(value, nameof(value.NodeDisconnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
            }
        }
    }

    public FpgaViewModelBase? SelectedFpgaViewModel
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool HideExtensions
    {
        get;
        set => SetProperty(ref field, value);
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsLoading = true;

            var file = Project.GetFile(Project.TopEntity);
            if (file == null) return;

            var nodeProvider = _fpgaService.GetNodeProviderByExtension(file.Extension);

            if (nodeProvider == null)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    .Error($"No node provider found for extension {file.Extension}");
                return;
            }

            var nodesEnumerable = await nodeProvider.ExtractNodesAsync(file);

            _nodes = nodesEnumerable.ToArray();
            RefreshHardware();

            SelectedFpgaPackage = FpgaPackages.FirstOrDefault(x => x.Name == Project.Properties.GetString("fpga")) ??
                                  FpgaPackages.FirstOrDefault();

            IsDirty = false;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error("Error initializing pin planner", e);
        }
        finally
        {
            IsLoading = false;
        }
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
        foreach (var package in _fpgaService.FpgaPackages) FpgaPackages.Add(package);

        if (oldSelectedFpgaPackageName != null)
            SelectedFpgaPackage = FpgaPackages.FirstOrDefault(x => x.Name == oldSelectedFpgaPackageName);

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
            Project.Properties.SetString("fpga", SelectedFpgaModel.Fpga.Name);
            Toolchain?.SaveConnections(Project, SelectedFpgaModel);
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
        if (SelectedFpgaModel == null) return;
        
        SaveAndClose(window);
        _ = _fpgaService.RunToolchainAsync(Project, SelectedFpgaModel);
    }
}