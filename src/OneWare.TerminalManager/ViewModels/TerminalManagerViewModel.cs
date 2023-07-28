using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using OneWare.Shared;
using OneWare.Shared.Services;
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.Models;

namespace OneWare.TerminalManager.ViewModels;

public class TerminalManagerViewModel : ExtendedTool
{
    public const string IconKey = "Material.Console";

    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    
    public ObservableCollection<TerminalTabModel> Terminals { get; } = new();

    private TerminalTabModel? _selectedTerminalTab;
    public TerminalTabModel? SelectedTerminalTab
    {
        get => _selectedTerminalTab;
        set => SetProperty(ref _selectedTerminalTab, value);
    }
    
    public TerminalManagerViewModel(ISettingsService settingsService, IDockService dockService, IProjectExplorerService projectExplorerService) : base(IconKey)
    {
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        
        Title = "Terminal";
        Id = "Terminal";
        
        // settingsService.GetSettingObservable<string>("General_SelectedTheme").Throttle(TimeSpan.FromMilliseconds(5))
        //     .Subscribe(x => Dispatcher.UIThread.Post(() =>
        //     {
        //         foreach (var t in Terminals)
        //         {
        //             t.Terminal.Redraw();
        //         } 
        //     }));
        
        NewTerminal();
    }

    public override void OnSelected()
    {
        base.OnSelected();
        if(!Terminals.Any()) NewTerminal();
    }

    public void CloseTab(TerminalTabModel tab)
    {
        Terminals.Remove(tab);

        if (!Terminals.Any())
        {
            _dockService.CloseDockable(this);
            return;
        }
        
        //Update Titles temporary
        for (var i = 0; i < Terminals.Count; i++)
        {
            if (i == 0) Terminals[i].Title = "Local";
            else Terminals[i].Title = $"Local ({i})";
        }
    }

    public void NewTerminal()
    {
        var homeFolder = _projectExplorerService.ActiveProject?.ProjectPath;

        homeFolder ??= RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "%HOMEDRIVE%" : "$HOME";
        
        Terminals.Add(new TerminalTabModel($"Local ({Terminals.Count})", new TerminalViewModel(homeFolder), this));
        SelectedTerminalTab = Terminals.Last();
    }
}