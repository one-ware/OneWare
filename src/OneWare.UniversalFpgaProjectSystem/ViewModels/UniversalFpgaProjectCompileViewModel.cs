using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Parser;
using OneWare.UniversalFpgaProjectSystem.Services;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels;

public class UniversalFpgaProjectCompileViewModel : FlexibleWindowViewModelBase
{
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    private CompositeDisposable? _compositeDisposable;

    private bool _hideExtensions;

    private FpgaModel? _selectedFpgaModel;

    public UniversalFpgaProjectCompileViewModel(IWindowService windowService,
        IProjectExplorerService projectExplorerService, FpgaService fpgaService, UniversalFpgaProjectRoot project)
    {
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
        Project = project;

        TopRightExtension = windowService.GetUiExtensions("CompileWindow_TopRightExtension");

        this.WhenValueChanged(x => x.IsDirty).Subscribe(x =>
        {
            Title = $"Connect and Compile - {Project.Header}{(x ? "*" : "")}";
        });

        //Construct FpgaModels
        foreach (var fpga in fpgaService.Fpgas)
        {
            fpgaService.CustomFpgaViewModels.TryGetValue(fpga, out var custom);
            custom ??= typeof(FpgaModel);
            var model = Activator.CreateInstance(custom, fpga) as FpgaModel;
            if (model is null) throw new NullReferenceException(nameof(model));
            FpgaModels.Add(model);
        }

        if (Project.TopEntity is IProjectFile file)
        {
            var provider = fpgaService.GetNodeProvider(file.Extension);
            if (provider is not null)
            {
                var nodes = provider.ExtractNodes(file).ToArray();
                foreach (var fpga in FpgaModels)
                foreach (var nodeModel in nodes)
                    fpga.AddNode(nodeModel);
            }
        }

        SelectedFpgaModel = FpgaModels.FirstOrDefault(x => x.Fpga.Name == project.GetProjectProperty("Fpga")) ??
                            FpgaModels.FirstOrDefault();

        IsDirty = false;
    }

    public ObservableCollection<UiExtension> TopRightExtension { get; }

    public UniversalFpgaProjectRoot Project { get; }
    public ObservableCollection<FpgaModel> FpgaModels { get; } = new();

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
                Project.Toolchain?.LoadConnections(Project, value);

                Observable.FromEventPattern(value, nameof(value.NodeConnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
                Observable.FromEventPattern(value, nameof(value.NodeDisconnected)).Subscribe(_ => { IsDirty = true; })
                    .DisposeWith(_compositeDisposable);
            }
        }
    }

    public bool HideExtensions
    {
        get => _hideExtensions;
        set => SetProperty(ref _hideExtensions, value);
    }

    public override void Close(FlexibleWindow window)
    {
        if (!IsDirty) window.Close();
        else _ = SafeQuitAsync(window);
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
                window.Close();
                return;
            case MessageBoxStatus.Canceled:
                return;
        }
    }

    public void SaveAndCompile(FlexibleWindow window)
    {
        SaveAndClose(window);
        if (SelectedFpgaModel != null) _ = Project.RunToolchainAsync(SelectedFpgaModel);
    }

    public void SaveAndClose(FlexibleWindow window)
    {
        if (SelectedFpgaModel != null)
        {
            FpgaSettingsParser.WriteDefaultSettingsIfEmpty(Project, SelectedFpgaModel.Fpga);
            Project.SetProjectProperty("Fpga", SelectedFpgaModel.Fpga.Name);
            Project.Toolchain?.SaveConnections(Project, SelectedFpgaModel);
            _ = _projectExplorerService.SaveProjectAsync(Project);
        }

        IsDirty = false;
        window.Close();
    }
}