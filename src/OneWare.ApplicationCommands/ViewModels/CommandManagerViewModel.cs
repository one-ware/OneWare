using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using OneWare.ApplicationCommands.Tabs;
using OneWare.ApplicationCommands.Views;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ApplicationCommands.ViewModels;

public partial class CommandManagerViewModel : FlexibleWindowViewModelBase
{
    private const int MaxFileResults = 200;
    private const int MinSearchLength = 2;

    private readonly IApplicationCommandService _applicationCommandService;

    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    [ObservableProperty] private CommandManagerTabBase _selectedTab;

    private readonly CommandManagerAllTab _allTab;
    private readonly CommandManagerFilesTab _filesTab;
    private readonly CommandManagerActionsTab _actionsTab;
    private readonly IReadOnlyCollection<IApplicationCommand> _staticCommands;
    private CancellationTokenSource _fileSearchCancellation = new();

    public CommandManagerViewModel(ILogical logical, IApplicationCommandService commandService,
        IProjectExplorerService projectExplorerService, IWindowService windowService)
    {
        ActiveFocus = logical;
        _projectExplorerService = projectExplorerService;
        _applicationCommandService = commandService;
        _windowService = windowService;
        _staticCommands = commandService.ApplicationCommands.ToList();

        _allTab = new CommandManagerAllTab(logical)
        {
            Items = new ObservableCollection<IApplicationCommand>(_staticCommands)
        };
        Tabs.Add(_allTab);
        _filesTab = new CommandManagerFilesTab(logical)
        {
            Items = new ObservableCollection<IApplicationCommand>()
        };
        Tabs.Add(_filesTab);
        _actionsTab = new CommandManagerActionsTab(logical)
        {
            Items = commandService.ApplicationCommands
        };
        Tabs.Add(_actionsTab);

        _selectedTab = Tabs.Last();

        _allTab.WhenValueChanged(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(text => _ = UpdateFileSearchAsync(_allTab, text));
        _filesTab.WhenValueChanged(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(150))
            .Subscribe(text => _ = UpdateFileSearchAsync(_filesTab, text));
    }

    public static KeyGesture ChangeShortcutGesture => new(Key.Enter, PlatformHelper.ControlKey);

    public ILogical ActiveFocus { get; }

    public bool IsSearching
    {
        get;
        set => SetProperty(ref field, value);
    }
    
    public ObservableCollection<CommandManagerTabBase> Tabs { get; } = new();

    private async Task UpdateFileSearchAsync(CommandManagerTabBase tab, string? searchText)
    {
        await _fileSearchCancellation.CancelAsync();
        _fileSearchCancellation.Dispose();
        _fileSearchCancellation = new CancellationTokenSource();
        
        var token = _fileSearchCancellation.Token;

        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length < MinSearchLength)
        {
            await Dispatcher.UIThread.InvokeAsync(() => ApplyFileCommands(tab, []));
            return;
        }

        IsSearching = true;
        var results = await Task.Run(() => FindFileCommands(searchText, token), token);
        IsSearching = false;
        if (token.IsCancellationRequested) return;
        
        await Dispatcher.UIThread.InvokeAsync(() => ApplyFileCommands(tab, results));
    }

    private void ApplyFileCommands(CommandManagerTabBase tab, IReadOnlyList<IApplicationCommand> fileCommands)
    {
        if (tab == _filesTab)
        {
            _filesTab.Items.Clear();
            _filesTab.Items.AddRange(fileCommands);
            _filesTab.RefreshVisibleItems();
            return;
        }

        if (tab == _allTab)
        {
            _allTab.Items.Clear();
            _allTab.Items.AddRange(fileCommands);
            _allTab.Items.AddRange(_staticCommands);
            _allTab.RefreshVisibleItems();
        }
    }

    private IReadOnlyList<IApplicationCommand> FindFileCommands(string searchText, CancellationToken token)
    {
        var results = new List<(int Score, string FullPath, string relativeToProject)>();
        var searchComparison = StringComparison.OrdinalIgnoreCase;

        foreach (var project in _projectExplorerService.Projects)
        {
            foreach (var relativePath in project.GetFiles())
            {
                if (token.IsCancellationRequested) return Array.Empty<IApplicationCommand>();
                
                var relativePathWithProject = Path.Combine(project.Name, relativePath);
                
                if (!relativePathWithProject.Contains(searchText, searchComparison)) continue;
                
                var score = ScoreFileMatch(relativePathWithProject, searchText);

                if (score <= 0) continue;

                var fullPath = Path.Combine(project.FullPath, relativePath);
                results.Add((score, fullPath, relativePathWithProject));
            }
        }

        var sorted = results
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.FullPath, ExplorerNameComparer.Instance)
            .Take(MaxFileResults)
            .Select(x => (IApplicationCommand)new OpenFileApplicationCommand(x.FullPath)
            {
                Detail = x.relativeToProject
            })
            .ToList();

        return sorted;
    }

    private static int ScoreFileMatch(string relativePath, string searchText)
    {
        var fileName = Path.GetFileName(relativePath);
        var comparison = StringComparison.OrdinalIgnoreCase;
        var score = 0;

        if (fileName.StartsWith(searchText, comparison)) score += 3;
        else if (fileName.Contains(searchText, comparison)) score += 2;
        else if (relativePath.Contains(searchText, comparison)) score += 1;

        return score;
    }

    public void ExecuteSelection(FlexibleWindow window)
    {
        if (SelectedTab?.SelectedItem?.Command.Execute(ActiveFocus) ?? false)
            Close(window);
    }

    [RelayCommand]
    private async Task ChangeShortcutAsync(FlexibleWindow window)
    {
        if (SelectedTab is not CommandManagerActionsTab || SelectedTab.SelectedItem == null) return;

        window.CloseOnDeactivated = false;

        await _windowService.ShowDialogAsync(new AssignGestureView
        {
            DataContext = new AssignGestureViewModel(SelectedTab.SelectedItem.Command)
        }, window.Host);

        _applicationCommandService.SaveKeyConfiguration();

        window.Host?.Activate();
        window.CloseOnDeactivated = true;
    }
}
