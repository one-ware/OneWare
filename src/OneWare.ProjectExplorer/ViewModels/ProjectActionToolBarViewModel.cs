using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;

namespace OneWare.ProjectExplorer.ViewModels;

/// <summary>
/// The shared Build / Run / Program / Debug buttons for the active project.
/// </summary>
public partial class ProjectActionToolBarViewModel : ObservableObject
{
    private readonly IProjectActionService _actionService;
    private readonly IProjectExplorerService _projectExplorerService;

    [ObservableProperty] private bool _canBuild;
    [ObservableProperty] private bool _canDebug;
    [ObservableProperty] private bool _canProgram;
    [ObservableProperty] private bool _canRun;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private string? _runningActionText;

    public ProjectActionToolBarViewModel(IProjectActionService actionService,
        IProjectExplorerService projectExplorerService)
    {
        _actionService = actionService;
        _projectExplorerService = projectExplorerService;

        actionService.StateChanged += (_, _) => Dispatcher.UIThread.Post(Refresh);
        projectExplorerService.WhenValueChanged(x => x.ActiveProject).Subscribe(_ => Refresh());
    }

    public void Refresh()
    {
        CanBuild = IsShown(ProjectActionKind.Build);
        CanRun = IsShown(ProjectActionKind.Run);
        CanProgram = IsShown(ProjectActionKind.Program);
        CanDebug = IsShown(ProjectActionKind.Debug);
        IsVisible = CanBuild || CanRun || CanProgram || CanDebug;
        IsBusy = _actionService.IsBusy;
        RunningActionText = _actionService.CurrentAction is { } action ? $"{action.Kind}..." : null;
    }

    private bool IsShown(ProjectActionKind kind)
    {
        return _projectExplorerService.ActiveProject is { } project
               && _actionService.GetProvider(project, kind) is { UsesSharedToolBar: true };
    }

    [RelayCommand]
    private Task BuildAsync()
    {
        return _actionService.ExecuteAsync(ProjectActionKind.Build);
    }

    [RelayCommand]
    private Task CleanAsync()
    {
        return _actionService.ExecuteAsync(ProjectActionKind.Clean);
    }

    [RelayCommand]
    private Task RunAsync()
    {
        return _actionService.ExecuteAsync(ProjectActionKind.Run);
    }

    [RelayCommand]
    private Task ProgramAsync()
    {
        return _actionService.ExecuteAsync(ProjectActionKind.Program);
    }

    [RelayCommand]
    private Task DebugAsync()
    {
        return _actionService.ExecuteAsync(ProjectActionKind.Debug);
    }

    [RelayCommand]
    private void Cancel()
    {
        _actionService.Cancel();
    }
}
