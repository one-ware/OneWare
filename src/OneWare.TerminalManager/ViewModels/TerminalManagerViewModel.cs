using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Terminal.ViewModels;
using OneWare.TerminalManager.Models;

namespace OneWare.TerminalManager.ViewModels;

public class TerminalManagerViewModel : ExtendedTool
{
    public const string IconKey = "Material.Console";

    private readonly IDockService _dockService;
    private readonly IPaths _paths;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogger _logger;

    private TerminalTabModel? _selectedTerminalTab;

    public TerminalManagerViewModel(
        ISettingsService settingsService,
        IDockService dockService,
        IProjectExplorerService projectExplorerService,
        IPaths paths,
        ILogger logger
    ) : base(IconKey)
    {
        _dockService = dockService;
        _paths = paths;
        _projectExplorerService = projectExplorerService;
        _logger = logger;

        Title = "Terminal";
        Id = "Terminal";

        settingsService.GetSettingObservable<string>("General_SelectedTheme")
            .Skip(1)
            .Throttle(TimeSpan.FromMilliseconds(5))
            .Subscribe(_ => Dispatcher.UIThread.Post(() =>
            {
                foreach (var t in Terminals)
                    t.Terminal.Redraw();
            }));
    }

    public ObservableCollection<TerminalTabModel> Terminals { get; } = new();

    public TerminalTabModel? SelectedTerminalTab
    {
        get => _selectedTerminalTab;
        set => SetProperty(ref _selectedTerminalTab, value);
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        NewTerminal();
    }

    public override void OnSelected()
    {
        base.OnSelected();
        if (!Terminals.Any())
            NewTerminal();
    }

    public void CloseTab(TerminalTabModel tab)
    {
        Terminals.Remove(tab);

        if (!Terminals.Any())
        {
            _dockService.CloseDockable(this);
            return;
        }

        for (var i = 0; i < Terminals.Count; i++)
        {
            Terminals[i].Title = i == 0 ? "Local" : $"Local ({i})";
        }
    }

    public void NewTerminal()
    {
        var homeFolder = _projectExplorerService.ActiveProject?.FullPath ?? _paths.ProjectsDirectory;

        Terminals.Add(new TerminalTabModel(
            $"Local ({Terminals.Count})",
            new TerminalViewModel(homeFolder),
            this
        ));

        SelectedTerminalTab = Terminals.Last();
    }

    public void ExecScriptInTerminal(string scriptPath, bool elevated, string title)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new NotImplementedException("Script execution not implemented for Windows.");

            PlatformHelper.ExecBash("chmod u+x " + scriptPath);

            var sudo = elevated ? "sudo " : "";
            var terminal = new TerminalViewModel(_paths.DocumentsDirectory);
            var wrapper = new StandaloneTerminalViewModel(title, terminal);

            _dockService.Show(wrapper);

            Observable.FromEventPattern(terminal, nameof(terminal.TerminalReady))
                .Take(1)
                .Delay(TimeSpan.FromMilliseconds(100))
                .Subscribe(_ => terminal.Send($"{sudo}{scriptPath}"));
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }
}
