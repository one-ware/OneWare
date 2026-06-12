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

        // Determine if required settings are missing — show setup overlay before loading
        IsSetupRequired = Toolchain == null || Project.TopEntity == null;

        if (IsSetupRequired)
            _ = LoadSetupOptionsAsync();
        else
            _ = InitializeAsync();
    }

    // ── Setup overlay ──────────────────────────────────────────────────────────

    public bool IsSetupRequired
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsSetupLoading
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ObservableCollection<string> SetupAvailableToolchains { get; } = new();
    public ObservableCollection<string> SetupAvailableTopEntities { get; } = new();

    public string? SetupSelectedToolchain
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? SetupSelectedTopEntity
    {
        get;
        set => SetProperty(ref field, value);
    }

    private async Task LoadSetupOptionsAsync()
    {
        IsSetupLoading = true;
        try
        {
            foreach (var tc in _fpgaService.Toolchains)
                SetupAvailableToolchains.Add(tc.Id);
            SetupSelectedToolchain = Toolchain?.Name ?? SetupAvailableToolchains.FirstOrDefault();

            var entities = await _fpgaService.GetAllTopEntitiesAsync(Project);
            foreach (var e in entities)
                SetupAvailableTopEntities.Add(e);
            SetupSelectedTopEntity = Project.TopEntity ?? SetupAvailableTopEntities.FirstOrDefault();
        }
        finally
        {
            IsSetupLoading = false;
        }
    }

    public async Task ApplySetupAsync()
    {
        if (SetupSelectedToolchain != null)
            Project.Toolchain = SetupSelectedToolchain;
        if (SetupSelectedTopEntity != null)
            Project.TopEntity = SetupSelectedTopEntity;

        await _projectExplorerService.SaveProjectAsync(Project);

        // Re-resolve the toolchain object from the updated project setting
        Toolchain = _fpgaService.Toolchains.FirstOrDefault(x => x.Id == Project.Toolchain);

        IsSetupRequired = false;
        await InitializeAsync();
    }

    // ── Main planner ───────────────────────────────────────────────────────────

    public IFpgaToolchain? Toolchain
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            UpdateActivePinProperties();
        }
    }

    /// <summary>
    /// Per-pin property definitions for the current hardware+toolchain combination.
    /// Prefers properties declared in the hardware JSON (<c>allowedPinProperties</c>);
    /// falls back to the toolchain's own <see cref="IFpgaToolchain.PinProperties"/> list.
    /// </summary>
    public IReadOnlyList<PinPropertyDefinition> ActivePinProperties
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

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

            UpdateActivePinProperties();

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
                Observable.FromEventPattern(value, nameof(value.PinPropertyChanged)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
            }
        }
    }

    /// <summary>
    /// Recomputes <see cref="ActivePinProperties"/>: hardware-defined properties take precedence,
    /// falling back to the toolchain's own list.
    /// </summary>
    private void UpdateActivePinProperties()
    {
        var hardwareProps = SelectedFpgaModel?.AllowedPinProperties;
        ActivePinProperties = hardwareProps is { Count: > 0 }
            ? hardwareProps
            : (IReadOnlyList<PinPropertyDefinition>)(Toolchain?.PinProperties ?? []).ToArray();
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

            if (Project.TopEntity is not { } entityName)
                return;

            var (topFile, nodeProvider) = await _fpgaService.FindTopEntityAsync(Project, entityName);

            if (topFile == null || nodeProvider == null)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    .Error($"Could not find a file containing top entity '{entityName}'");
                return;
            }

            var nodesEnumerable = await nodeProvider.ExtractNodesAsync(topFile, entityName);
            _nodes = nodesEnumerable.ToArray();

            RefreshHardware();

            SelectedFpgaPackage = FpgaPackages.FirstOrDefault(x => x.Name == Project.Board) ??
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
        
        RefreshHardware();
    }

    private void RefreshHardware()
    {
        _fpgaService.LoadGenericHardware();
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
            Project.Board = SelectedFpgaModel.Fpga.Name;
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