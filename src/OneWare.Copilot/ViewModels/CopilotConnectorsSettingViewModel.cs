using System.Collections.ObjectModel;
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

/// <summary>Settings page listing the connectors (IDE-wide MCP servers).</summary>
public sealed class CopilotConnectorsSettingViewModel : ObservableObject
{
    private readonly CopilotConnectorService _service;
    private bool _isLoading;
    private bool _loaded;
    private string? _message;
    private bool _isError;

    public CopilotConnectorsSettingViewModel(CopilotConnectorService service)
    {
        _service = service;
        _service.SignedIn += (_, name) => Dispatcher.UIThread.Post(() =>
        {
            if (Connectors.FirstOrDefault(x => x.Name == name) is { } item)
                item.Status = new McpConnectorStatus(McpConnectorState.Connected);
        });

        AddCommand = new AsyncRelayCommand<Control?>(AddAsync);
        ImportCommand = new AsyncRelayCommand<Control?>(ImportAsync);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(true));
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
        private set => SetProperty(ref _message, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public bool IsEmpty => !IsLoading && Connectors.Count == 0;

    public string ConfigFile => System.IO.Path.Combine(
        Services.CopilotSessionConfiguration.GetConfigDirectory(), "mcp-config.json");

    public IAsyncRelayCommand<Control?> AddCommand { get; }

    public IAsyncRelayCommand<Control?> ImportCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    internal CopilotConnectorService Service => _service;

    /// <summary>Loads the list the first time the page is shown.</summary>
    public void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _ = LoadAsync(false);
    }

    private async Task LoadAsync(bool force)
    {
        if (IsLoading) return;
        IsLoading = true;
        OnPropertyChanged(nameof(IsEmpty));
        if (force) ShowMessage(null);

        try
        {
            var connectors = await _service.ListAsync();
            var stored = await _service.GetStoredVariablesAsync();

            Connectors.Clear();
            foreach (var connector in connectors)
                Connectors.Add(new CopilotConnectorItemViewModel(this, connector));

            foreach (var item in Connectors.ToList()) await item.UpdateVariablesAsync(stored);
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
        }

        // Remote connectors are cheap to check; local ones would run their command, so they are checked on demand.
        foreach (var item in Connectors.Where(x => x is { IsRemote: true, Enabled: true }).ToList())
            await item.CheckAsync();
    }

    internal void ShowMessage(string? message, bool isError = false)
    {
        Message = message;
        IsError = isError;
    }

    private async Task AddAsync(Control? owner)
    {
        var editor = await CopilotConnectorEditorViewModel.CreateAsync(_service, null,
            Connectors.Select(x => x.Name));
        if (await ShowEditorAsync(editor, owner)) await LoadAsync(true);
    }

    internal async Task EditAsync(CopilotConnectorItemViewModel item, Control? owner)
    {
        var editor = await CopilotConnectorEditorViewModel.CreateAsync(_service, item.Connector,
            Connectors.Where(x => x != item).Select(x => x.Name));
        if (await ShowEditorAsync(editor, owner)) await LoadAsync(true);
    }

    private static async Task<bool> ShowEditorAsync(CopilotConnectorEditorViewModel editor, Control? owner)
    {
        var window = new CopilotConnectorEditorView { DataContext = editor };
        await ContainerLocator.Container.Resolve<IWindowService>()
            .ShowDialogAsync(window, owner == null ? null : TopLevel.GetTopLevel(owner) as Window);
        return editor.Saved;
    }

    internal async Task RemoveAsync(CopilotConnectorItemViewModel item, Control? owner)
    {
        var result = await ContainerLocator.Container.Resolve<IWindowService>().ShowYesNoAsync("Remove Connector",
            $"Remove the connector '{item.Name}'? It is removed from the Copilot CLI configuration as well.",
            MessageBoxIcon.Warning, owner == null ? null : TopLevel.GetTopLevel(owner) as Window);
        if (result != MessageBoxStatus.Yes) return;

        try
        {
            await _service.RemoveAsync(item.Connector);
            Connectors.Remove(item);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception e)
        {
            ShowMessage($"Failed to remove '{item.Name}': {e.Message}", true);
        }
    }

    private async Task ImportAsync(Control? owner)
    {
        if (owner == null || TopLevel.GetTopLevel(owner) is not { } topLevel) return;

        var file = await StorageProviderHelper.SelectFileAsync(topLevel, "Import MCP Servers", null,
            new FilePickerFileType("MCP configuration") { Patterns = ["*.json"] });
        if (file == null) return;

        var warnings = new List<string>();
        var imported = McpConnectorImport.Read(file, warnings.Add);
        // Names with the same variable key would share stored values.
        var existing = Connectors.Select(x => McpConnector.GetVariableKey(x.Name)).ToHashSet(StringComparer.Ordinal);
        var added = new List<string>();

        foreach (var (connector, clientSecret) in imported)
        {
            if (!existing.Add(McpConnector.GetVariableKey(connector.Name)))
            {
                warnings.Add($"'{connector.Name}' already exists or clashes with a similar name and was skipped.");
                continue;
            }

            try
            {
                await _service.SaveAsync(connector);
                if (!string.IsNullOrEmpty(clientSecret))
                    await _service.SetOAuthClientSecretAsync(connector.Name, clientSecret);
                added.Add(connector.Name);
            }
            catch (Exception e)
            {
                warnings.Add($"'{connector.Name}' could not be added: {e.Message}");
            }
        }

        await LoadAsync(true);

        var summary = added.Count == 0
            ? "No connectors were imported."
            : $"Imported {string.Join(", ", added)}.";
        if (warnings.Count > 0) summary += " " + string.Join(" ", warnings);
        ShowMessage(summary, warnings.Count > 0 && added.Count == 0);
    }
}

public sealed class CopilotConnectorItemViewModel : ObservableObject
{
    private readonly CopilotConnectorsSettingViewModel _owner;
    private bool _enabled;
    private bool _isBusy;
    private McpConnectorStatus? _status;
    private string? _missingVariables;

    public CopilotConnectorItemViewModel(CopilotConnectorsSettingViewModel owner, McpConnector connector)
    {
        _owner = owner;
        Connector = connector;
        _enabled = connector.Enabled;

        EditCommand = new AsyncRelayCommand<Control?>(x => _owner.EditAsync(this, x));
        RemoveCommand = new AsyncRelayCommand<Control?>(x => _owner.RemoveAsync(this, x));
        CheckCommand = new AsyncRelayCommand(CheckAsync);
        SignInCommand = new AsyncRelayCommand(SignInAsync);
    }

    public McpConnector Connector { get; }

    public string Name => Connector.Name;

    public string Description => Connector.Describe();

    public bool IsRemote => Connector.Kind == McpConnectorKind.Remote;

    public string KindText => IsRemote ? "Remote" : "Local";

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (!SetProperty(ref _enabled, value)) return;
            Connector.Enabled = value;
            Status = null;
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
            OnPropertyChanged(nameof(HasStatus));
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(NeedsSignIn));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(CanSignIn));
        }
    }

    public bool HasStatus => Status != null && Status.State != McpConnectorState.Unknown;

    public bool IsConnected => Status?.State == McpConnectorState.Connected;

    public bool NeedsSignIn => Status?.State == McpConnectorState.NeedsSignIn;

    public bool IsFailed => Status?.State == McpConnectorState.Failed;

    public bool CanSignIn => IsRemote && Enabled && !IsConnected;

    public string StatusText => Status?.State switch
    {
        McpConnectorState.Connected => "Connected",
        McpConnectorState.NeedsSignIn => "Sign-in required",
        McpConnectorState.Failed => "Failed",
        _ => string.Empty
    };

    public string? StatusMessage => Status?.Message;

    public string? MissingVariables
    {
        get => _missingVariables;
        private set => SetProperty(ref _missingVariables, value);
    }

    public IAsyncRelayCommand<Control?> EditCommand { get; }

    public IAsyncRelayCommand<Control?> RemoveCommand { get; }

    public IAsyncRelayCommand CheckCommand { get; }

    public IAsyncRelayCommand SignInCommand { get; }

    internal async Task UpdateVariablesAsync(IReadOnlySet<string> stored)
    {
        var missing = new List<string>();
        foreach (var variable in Connector.GetVariables())
            if (!stored.Contains(variable) &&
                await _owner.Service.GetVariableSourceAsync(variable) == McpVariableSource.Missing)
                missing.Add(variable);

        MissingVariables = missing.Count == 0 ? null : $"Not set: {string.Join(", ", missing)}";
    }

    internal async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Status = await _owner.Service.CheckAsync(Connector);
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
            Status = await _owner.Service.SignInAsync(Connector);
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
            await _owner.Service.SetEnabledAsync(Name, enabled);
            OnPropertyChanged(nameof(CanSignIn));
            if (enabled && IsRemote) await CheckAsync();
        }
        catch (Exception e)
        {
            _owner.ShowMessage($"Failed to {(enabled ? "enable" : "disable")} '{Name}': {e.Message}", true);
        }
    }
}
