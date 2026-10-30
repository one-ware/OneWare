using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Copilot.Connectors;
using OneWare.Copilot.Views;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.ViewModels;

/// <summary>Lists the connectors (IDE-wide MCP servers), in the settings and in a window opened from the chat.</summary>
public sealed class CopilotConnectorsSettingViewModel : ObservableObject
{
    private readonly McpConnectorHub _hub;
    private bool _isLoading;
    private bool _loaded;
    private string? _message;
    private bool _isError;
    private int _cliImportCount;

    public CopilotConnectorsSettingViewModel(McpConnectorHub hub)
    {
        _hub = hub;

        AddCommand = new AsyncRelayCommand<Control?>(AddAsync);
        ImportCommand = new AsyncRelayCommand<Control?>(ImportFileAsync);
        ImportCliCommand = new AsyncRelayCommand(ImportCliAsync);
        OpenFileCommand = new RelayCommand(OpenFile);
        DismissMessageCommand = new RelayCommand(() => ShowMessage(null));
    }

    public ObservableCollection<CopilotConnectorItemViewModel> Connectors { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(HasMessage));
        }
    }

    public bool HasMessage => !string.IsNullOrEmpty(Message);

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public bool IsEmpty => !IsLoading && Connectors.Count == 0;

    /// <summary>Servers in the Copilot CLI's configuration that are not connectors yet.</summary>
    public int CliImportCount
    {
        get => _cliImportCount;
        private set
        {
            if (!SetProperty(ref _cliImportCount, value)) return;
            OnPropertyChanged(nameof(CanImportCli));
            OnPropertyChanged(nameof(CliImportText));
        }
    }

    public bool CanImportCli => CliImportCount > 0;

    public string CliImportText => CliImportCount == 1
        ? "1 MCP server from the Copilot CLI configuration is not used in OneWare."
        : $"{CliImportCount} MCP servers from the Copilot CLI configuration are not used in OneWare.";

    public string? LoadError => _hub.LoadError == null ? null : $"mcp.json has errors: {_hub.LoadError}";

    public string ConfigFile => _hub.ConfigFile;

    public IAsyncRelayCommand<Control?> AddCommand { get; }

    public IAsyncRelayCommand<Control?> ImportCommand { get; }

    public IAsyncRelayCommand ImportCliCommand { get; }

    public IRelayCommand OpenFileCommand { get; }

    public IRelayCommand DismissMessageCommand { get; }

    internal McpConnectorHub Hub => _hub;

    /// <summary>Opens the list in its own window, e.g. from the chat.</summary>
    public static void ShowWindow(McpConnectorHub hub, Control? owner)
    {
        var window = new CopilotConnectorsWindow { DataContext = new CopilotConnectorsSettingViewModel(hub) };
        _ = ContainerLocator.Container.Resolve<IWindowService>()
            .ShowDialogAsync(window, owner == null ? null : TopLevel.GetTopLevel(owner) as Window);
    }

    /// <summary>Loads the list and follows changes while it is shown.</summary>
    public void Attach()
    {
        if (_loaded) return;
        _loaded = true;
        _hub.Changed += OnHubChanged;
        _hub.StatusChanged += OnHubStatusChanged;
        _ = LoadAsync();
    }

    public void Detach()
    {
        if (!_loaded) return;
        _loaded = false;
        _hub.Changed -= OnHubChanged;
        _hub.StatusChanged -= OnHubStatusChanged;
    }

    private void OnHubChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => _ = LoadAsync());

    private void OnHubStatusChanged(object? sender, string name) => Dispatcher.UIThread.Post(() =>
    {
        if (Connectors.FirstOrDefault(x => x.Name == name) is { } item) item.Status = _hub.GetStatus(name);
    });

    private async Task LoadAsync()
    {
        if (!_loaded) return;
        IsLoading = true;
        OnPropertyChanged(nameof(IsEmpty));

        try
        {
            var connectors = await _hub.GetConnectorsAsync();

            // Keeps the items (and their busy state) of connectors that still exist.
            var items = connectors.Select(connector =>
            {
                var item = Connectors.FirstOrDefault(x => x.Connector.Id == connector.Id) ??
                           new CopilotConnectorItemViewModel(this);
                item.SetConnector(connector, _hub.GetStatus(connector.Name));
                return item;
            }).ToList();

            Connectors.Clear();
            foreach (var item in items) Connectors.Add(item);

            var names = connectors.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            CliImportCount = (await _hub.ReadCopilotCliServersAsync()).Count(x => !names.Contains(x.Connector.Name));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().LogWarning(e, "Failed to load the connectors.");
            ShowMessage(e.Message, true);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(LoadError));
        }
    }

    internal void ShowMessage(string? message, bool isError = false)
    {
        Message = message;
        IsError = isError;
    }

    private async Task AddAsync(Control? owner)
    {
        var editor = await CopilotConnectorEditorViewModel.CreateAsync(_hub, null);
        await ShowEditorAsync(editor, owner);
    }

    internal async Task EditAsync(CopilotConnectorItemViewModel item, Control? owner)
    {
        var editor = await CopilotConnectorEditorViewModel.CreateAsync(_hub, item.Connector);
        await ShowEditorAsync(editor, owner);
    }

    private static Task ShowEditorAsync(CopilotConnectorEditorViewModel editor, Control? owner) =>
        ContainerLocator.Container.Resolve<IWindowService>().ShowDialogAsync(
            new CopilotConnectorEditorView { DataContext = editor },
            owner == null ? null : TopLevel.GetTopLevel(owner) as Window);

    internal async Task RemoveAsync(CopilotConnectorItemViewModel item, Control? owner)
    {
        var result = await ContainerLocator.Container.Resolve<IWindowService>().ShowYesNoAsync("Remove MCP Server",
            $"Remove '{item.Name}'? Its stored secrets are deleted as well.",
            MessageBoxIcon.Warning, owner == null ? null : TopLevel.GetTopLevel(owner) as Window);
        if (result != MessageBoxStatus.Yes) return;

        try
        {
            await _hub.RemoveAsync(item.Connector.Id);
        }
        catch (Exception e)
        {
            ShowMessage($"Failed to remove '{item.Name}': {e.Message}", true);
        }
    }

    private async Task ImportFileAsync(Control? owner)
    {
        if (owner == null || TopLevel.GetTopLevel(owner) is not { } topLevel) return;

        var file = await StorageProviderHelper.SelectFileAsync(topLevel, "Import MCP Servers", null,
            new FilePickerFileType("MCP configuration") { Patterns = ["*.json"] });
        if (file == null) return;

        var warnings = new List<string>();
        await ImportAsync(McpConnectorImport.Read(file, warnings.Add), warnings);
    }

    private async Task ImportCliAsync()
    {
        var warnings = new List<string>();
        var names = (await _hub.GetConnectorsAsync()).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var servers = (await _hub.ReadCopilotCliServersAsync(warnings.Add))
            .Where(x => !names.Contains(x.Connector.Name)).ToList();
        await ImportAsync(servers, warnings);
    }

    private async Task ImportAsync(IReadOnlyList<ImportedMcpConnector> servers, List<string> warnings)
    {
        IReadOnlyList<string> added = [];
        try
        {
            added = await _hub.AddAsync(servers, warnings.Add);
        }
        catch (Exception e)
        {
            warnings.Add(e.Message);
        }

        var summary = added.Count == 0 ? "Nothing was imported." : $"Imported {string.Join(", ", added)}.";
        if (warnings.Count > 0) summary += " " + string.Join(" ", warnings);
        ShowMessage(summary, added.Count == 0);
    }

    private void OpenFile()
    {
        try
        {
            if (!File.Exists(_hub.ConfigFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_hub.ConfigFile)!);
                File.WriteAllText(_hub.ConfigFile, "{\n  \"mcpServers\": {}\n}\n");
            }

            _ = ContainerLocator.Container.Resolve<IMainDockService>().OpenFileAsync(_hub.ConfigFile);
        }
        catch (Exception e)
        {
            ShowMessage($"Failed to open {_hub.ConfigFile}: {e.Message}", true);
        }
    }
}

public sealed class CopilotConnectorItemViewModel : ObservableObject
{
    private readonly CopilotConnectorsSettingViewModel _owner;
    private McpConnector _connector = new();
    private bool _enabled;
    private bool _isBusy;
    private McpConnectorStatus? _status;

    public CopilotConnectorItemViewModel(CopilotConnectorsSettingViewModel owner)
    {
        _owner = owner;
        EditCommand = new AsyncRelayCommand<Control?>(x => _owner.EditAsync(this, x));
        RemoveCommand = new AsyncRelayCommand<Control?>(x => _owner.RemoveAsync(this, x));
        CheckCommand = new AsyncRelayCommand(CheckAsync);
        SignInCommand = new AsyncRelayCommand(SignInAsync);
    }

    public McpConnector Connector => _connector;

    public string Name => _connector.Name;

    public string Description => _connector.Describe();

    public bool IsRemote => _connector.Kind == McpConnectorKind.Remote;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value)) return;
            OnPropertyChanged(nameof(CanSignIn));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(NeedsSignIn));
            OnPropertyChanged(nameof(HasStatusMessage));
            _ = ApplyEnabledAsync(value);
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public McpConnectorStatus? Status
    {
        get => _status;
        set
        {
            if (!SetProperty(ref _status, value)) return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(HasStatusMessage));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(NeedsSignIn));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(IsUnknown));
        }
    }

    public bool IsConnected => Status?.State == McpConnectorState.Connected;

    public bool NeedsSignIn => Enabled && Status?.State == McpConnectorState.NeedsSignIn;

    public bool IsFailed => Status?.State == McpConnectorState.Failed;

    public bool IsUnknown => !IsConnected && !NeedsSignIn && !IsFailed;

    public bool CanSignIn => IsRemote && Enabled;

    public string StatusText => !Enabled
        ? "Disabled"
        : Status?.State switch
        {
            McpConnectorState.Connected => "Connected",
            McpConnectorState.NeedsSignIn => "Sign-in required",
            McpConnectorState.Failed => "Failed",
            McpConnectorState.Pending => "Starting…",
            _ => "Not checked"
        };

    public string? StatusMessage => Status?.Message;

    public bool HasStatusMessage => Enabled && !string.IsNullOrWhiteSpace(StatusMessage);

    public IAsyncRelayCommand<Control?> EditCommand { get; }

    public IAsyncRelayCommand<Control?> RemoveCommand { get; }

    public IAsyncRelayCommand CheckCommand { get; }

    public IAsyncRelayCommand SignInCommand { get; }

    internal void SetConnector(McpConnector connector, McpConnectorStatus? status)
    {
        _connector = connector;
        _enabled = connector.Enabled;
        OnPropertyChanged(string.Empty);
        Status = status;
    }

    private async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Status = await _owner.Hub.CheckAsync(await _owner.Hub.ResolveAsync(Connector), report: true);
        }
        catch (Exception e)
        {
            Status = new McpConnectorStatus(McpConnectorState.Failed, e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Status = await _owner.Hub.SignInAsync(Name);
        }
        catch (Exception e)
        {
            Status = new McpConnectorStatus(McpConnectorState.Failed, e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyEnabledAsync(bool enabled)
    {
        try
        {
            await _owner.Hub.SetEnabledAsync(Connector.Id, enabled);
        }
        catch (Exception e)
        {
            _owner.ShowMessage($"Failed to {(enabled ? "enable" : "disable")} '{Name}': {e.Message}", true);
        }
    }
}
