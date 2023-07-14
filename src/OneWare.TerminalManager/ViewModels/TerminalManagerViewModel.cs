using System.Collections.ObjectModel;
using System.Reactive.Linq;
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

    private IProjectExplorerService _projectExplorerService;
    
    public ObservableCollection<TerminalTabModel> Terminals { get; } = new();

    private TerminalTabModel? _selectedTerminalTab;
    public TerminalTabModel? SelectedTerminalTab
    {
        get => _selectedTerminalTab;
        set => SetProperty(ref _selectedTerminalTab, value);
    }
    
    public TerminalManagerViewModel(ISettingsService settingsService, IProjectExplorerService projectExplorerService) : base(IconKey)
    {
        _projectExplorerService = projectExplorerService;
        
        Title = "Terminal";
        Id = "Terminal";
        
        settingsService.GetSettingObservable<string>("General_SelectedTheme").Throttle(TimeSpan.FromMilliseconds(5))
            .Subscribe(x => Dispatcher.UIThread.Post(() =>
            {
                foreach (var t in Terminals)
                {
                    t.Terminal.Redraw();
                } 
            }));
        
        Terminals.Add(new TerminalTabModel("Local", new TerminalViewModel("C:/"), this));
    }
    
    public void CloseTab(TerminalTabModel? tab)
    {
        if(tab != null) Terminals.Remove(tab);
    }

    public void NewTerminal()
    {
        Terminals.Add(new TerminalTabModel($"Local {Terminals.Count}", new TerminalViewModel(_projectExplorerService.ActiveProject?.ProjectPath ?? "C:/"), this));
        SelectedTerminalTab = Terminals.Last();
    }
}