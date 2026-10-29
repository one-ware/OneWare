using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Copilot.Connectors;
using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;

namespace OneWare.Copilot.ViewModels;

/// <summary>Adds or edits a connector. Values marked as secret are kept in the OS credential store.</summary>
public sealed class CopilotConnectorEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly CopilotConnectorService _service;
    private readonly McpConnector? _original;
    private readonly HashSet<string> _otherNames;
    private readonly IReadOnlySet<string> _storedVariables;
    private string _name = string.Empty;
    private int _kindIndex;
    private string _command = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _url = string.Empty;
    private bool _useSse;
    private string _oauthClientId = string.Empty;
    private string _oauthClientSecret = string.Empty;
    private bool _hasStoredOAuthClientSecret;
    private bool _clearOAuthClientSecret;
    private string? _error;
    private bool _isSaving;

    private CopilotConnectorEditorViewModel(CopilotConnectorService service, McpConnector? original,
        IEnumerable<string> otherNames, IReadOnlySet<string> storedVariables)
    {
        _service = service;
        _original = original;
        // Names with the same variable key would share stored values.
        _otherNames = otherNames.Select(McpConnector.GetVariableKey).ToHashSet(StringComparer.Ordinal);
        _storedVariables = storedVariables;
        Title = original == null ? "Add Connector" : $"Edit Connector - {original.Name}";

        AddEnvCommand = new RelayCommand(() => AddValue(Env, new McpConnectorValue("", ""), false, false));
        AddHeaderCommand = new RelayCommand(() => AddValue(Headers, new McpConnectorValue("", ""), false, false));
        ClearOAuthClientSecretCommand = new RelayCommand(() =>
        {
            ClearOAuthClientSecret = true;
            OAuthClientSecret = string.Empty;
        });
        SaveCommand = new AsyncRelayCommand<FlexibleWindow?>(SaveAsync);
        CancelCommand = new RelayCommand<FlexibleWindow?>(x => x?.Close());

        if (original == null) return;

        _name = original.Name;
        _kindIndex = original.Kind == McpConnectorKind.Remote ? 1 : 0;
        _command = original.Command;
        _arguments = string.Join(Environment.NewLine, original.Args);
        _workingDirectory = original.WorkingDirectory ?? string.Empty;
        _url = original.Url;
        _useSse = original.UseSse;
        _oauthClientId = original.OAuthClientId ?? string.Empty;

        foreach (var value in original.Env) AddLoadedValue(Env, value);
        foreach (var value in original.Headers) AddLoadedValue(Headers, value);
    }

    public static async Task<CopilotConnectorEditorViewModel> CreateAsync(CopilotConnectorService service,
        McpConnector? connector, IEnumerable<string> otherNames)
    {
        var stored = await service.GetStoredVariablesAsync();
        var editor = new CopilotConnectorEditorViewModel(service, connector?.Clone(), otherNames, stored);
        if (connector != null)
            editor.HasStoredOAuthClientSecret = await service.HasOAuthClientSecretAsync(connector.Name);
        editor.UpdateVariables();
        return editor;
    }

    public bool Saved { get; private set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>0 = local command, 1 = remote URL.</summary>
    public int KindIndex
    {
        get => _kindIndex;
        set
        {
            if (!SetProperty(ref _kindIndex, value)) return;
            OnPropertyChanged(nameof(IsLocal));
            OnPropertyChanged(nameof(IsRemote));
            UpdateVariables();
        }
    }

    public bool IsLocal => KindIndex == 0;

    public bool IsRemote => KindIndex == 1;

    public string Command
    {
        get => _command;
        set
        {
            if (SetProperty(ref _command, value)) UpdateVariables();
        }
    }

    /// <summary>One argument per line.</summary>
    public string Arguments
    {
        get => _arguments;
        set
        {
            if (SetProperty(ref _arguments, value)) UpdateVariables();
        }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (SetProperty(ref _workingDirectory, value)) UpdateVariables();
        }
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public bool UseSse
    {
        get => _useSse;
        set => SetProperty(ref _useSse, value);
    }

    public string OAuthClientId
    {
        get => _oauthClientId;
        set => SetProperty(ref _oauthClientId, value);
    }

    public string OAuthClientSecret
    {
        get => _oauthClientSecret;
        set
        {
            if (SetProperty(ref _oauthClientSecret, value) && value.Length > 0) ClearOAuthClientSecret = false;
        }
    }

    public bool HasStoredOAuthClientSecret
    {
        get => _hasStoredOAuthClientSecret;
        private set
        {
            if (SetProperty(ref _hasStoredOAuthClientSecret, value))
                OnPropertyChanged(nameof(OAuthClientSecretWatermark));
        }
    }

    public bool ClearOAuthClientSecret
    {
        get => _clearOAuthClientSecret;
        private set
        {
            if (SetProperty(ref _clearOAuthClientSecret, value))
                OnPropertyChanged(nameof(OAuthClientSecretWatermark));
        }
    }

    public string OAuthClientSecretWatermark => HasStoredOAuthClientSecret && !ClearOAuthClientSecret
        ? "Stored - leave empty to keep"
        : "Only for confidential apps";

    public string RedirectUrl => CopilotConnectorService.OAuthRedirectUrl;

    public ObservableCollection<CopilotConnectorValueViewModel> Env { get; } = [];

    public ObservableCollection<CopilotConnectorValueViewModel> Headers { get; } = [];

    /// <summary>Variables referenced with <c>${VAR}</c> that are not secret values of this editor.</summary>
    public ObservableCollection<CopilotConnectorVariableViewModel> Variables { get; } = [];

    public bool HasVariables => Variables.Count > 0;

    public string? Error
    {
        get => _error;
        private set => SetProperty(ref _error, value);
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set => SetProperty(ref _isSaving, value);
    }

    public RelayCommand AddEnvCommand { get; }

    public RelayCommand AddHeaderCommand { get; }

    public RelayCommand ClearOAuthClientSecretCommand { get; }

    public AsyncRelayCommand<FlexibleWindow?> SaveCommand { get; }

    public RelayCommand<FlexibleWindow?> CancelCommand { get; }

    private void AddLoadedValue(ObservableCollection<CopilotConnectorValueViewModel> target, McpConnectorValue value)
    {
        // A value that is exactly this connector's generated variable is shown as a stored secret.
        var isSecret = McpConnector.IsVariableReference(value.Value, out var variable) &&
                       variable == McpConnector.GetSecretVariableName(_original!.Name, value.Key);
        AddValue(target, isSecret ? value with { Value = string.Empty } : value, isSecret,
            isSecret && _storedVariables.Contains(variable));
    }

    private void AddValue(ObservableCollection<CopilotConnectorValueViewModel> target, McpConnectorValue value,
        bool isSecret, bool hasStoredValue)
    {
        var row = new CopilotConnectorValueViewModel(value.Key, value.Value, isSecret, hasStoredValue,
            isSecret ? value.Key : null);
        row.RemoveCommand = new RelayCommand(() =>
        {
            target.Remove(row);
            UpdateVariables();
        });
        row.PropertyChanged += OnRowChanged;
        target.Add(row);
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CopilotConnectorValueViewModel.Value)
            or nameof(CopilotConnectorValueViewModel.IsSecret))
            UpdateVariables();
    }

    private McpConnector BuildConnector(bool forVariables)
    {
        var connector = _original?.Clone() ?? new McpConnector();
        connector.Name = Name.Trim();
        connector.Kind = IsRemote ? McpConnectorKind.Remote : McpConnectorKind.Local;
        connector.Command = Command.Trim();
        connector.Args = Arguments.Split('\n').Select(x => x.TrimEnd('\r')).Where(x => x.Length > 0).ToList();
        connector.WorkingDirectory = string.IsNullOrWhiteSpace(WorkingDirectory) ? null : WorkingDirectory.Trim();
        connector.Url = Url.Trim();
        connector.UseSse = UseSse;
        connector.OAuthClientId = string.IsNullOrWhiteSpace(OAuthClientId) ? null : OAuthClientId.Trim();

        McpConnectorValue ToValue(CopilotConnectorValueViewModel row) => new(row.Key.Trim(), row.IsSecret
            ? forVariables
                ? string.Empty
                : McpConnector.ToVariableReference(McpConnector.GetSecretVariableName(connector.Name, row.Key.Trim()))
            : row.Value);

        connector.Env = Env.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(ToValue).ToList();
        connector.Headers = Headers.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(ToValue).ToList();
        return connector;
    }

    private void UpdateVariables()
    {
        var names = BuildConnector(true).GetVariables();
        foreach (var removed in Variables.Where(x => !names.Contains(x.Name)).ToList()) Variables.Remove(removed);
        for (var i = 0; i < names.Count; i++)
        {
            if (Variables.Any(x => x.Name == names[i])) continue;
            var variable = new CopilotConnectorVariableViewModel(names[i], _storedVariables.Contains(names[i]));
            Variables.Insert(Math.Min(i, Variables.Count), variable);
            if (!variable.HasStoredValue) _ = ResolveSourceAsync(variable);
        }

        OnPropertyChanged(nameof(HasVariables));
    }

    private async Task ResolveSourceAsync(CopilotConnectorVariableViewModel variable)
    {
        var source = await _service.GetVariableSourceAsync(variable.Name);
        await Dispatcher.UIThread.InvokeAsync(() => variable.Source = source);
    }

    private string? Validate(McpConnector connector)
    {
        if (string.IsNullOrWhiteSpace(connector.Name)) return "Enter a name.";
        if (!McpConnector.IsValidName(connector.Name))
            return "The name needs a letter or digit and may only contain letters, digits, '.', '-' and '_'.";
        if (_otherNames.Contains(McpConnector.GetVariableKey(connector.Name)))
            return $"A connector named '{connector.Name}' or with a similar name already exists.";

        if (connector.Kind == McpConnectorKind.Local)
        {
            if (string.IsNullOrWhiteSpace(connector.Command)) return "Enter the command that starts the server.";
        }
        else if (!Uri.TryCreate(connector.Url, UriKind.Absolute, out var uri) ||
                 uri.Scheme is not ("http" or "https"))
        {
            return "Enter the server's http(s) URL.";
        }

        var rows = IsRemote ? Headers : Env;
        var keys = rows.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(x => x.Key.Trim()).ToList();
        if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Count)
            return IsRemote ? "Each header may only be set once." : "Each variable may only be set once.";

        var secretKeys = rows.Where(x => x.IsSecret && !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => McpConnector.GetVariableKey(x.Key)).ToList();
        if (secretKeys.Distinct(StringComparer.Ordinal).Count() != secretKeys.Count)
            return "Secret values need names that differ in more than case and punctuation.";

        var missingSecret = rows.FirstOrDefault(x =>
            x.IsSecret && !string.IsNullOrWhiteSpace(x.Key) && x.Value.Length == 0 &&
            !(x.HasStoredValue && x.Key.Trim() == x.StoredKey));
        return missingSecret != null ? $"Enter the secret value of '{missingSecret.Key.Trim()}'." : null;
    }

    private async Task SaveAsync(FlexibleWindow? window)
    {
        var connector = BuildConnector(false);
        Error = Validate(connector);
        if (Error != null) return;

        IsSaving = true;
        try
        {
            // Secret values keyed by the variables they are stored under; null removes a stored value.
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            var renames = new Dictionary<string, string>(StringComparer.Ordinal);
            var rows = IsRemote ? Headers : Env;
            var activeSecrets = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows.Where(x => !string.IsNullOrWhiteSpace(x.Key)))
            {
                if (!row.IsSecret) continue;
                var variable = McpConnector.GetSecretVariableName(connector.Name, row.Key.Trim());
                activeSecrets.Add(variable);
                if (row.Value.Length > 0) values[variable] = row.Value;
                else if (_original != null && row.StoredKey != null)
                    renames[McpConnector.GetSecretVariableName(_original.Name, row.StoredKey)] = variable;
            }

            // Secrets of removed rows, rows that are no longer secret and of the old name.
            if (_original != null)
                foreach (var variable in _original.Env.Concat(_original.Headers)
                             .Select(x => McpConnector.GetSecretVariableName(_original.Name, x.Key))
                             .Where(x => _storedVariables.Contains(x) && !activeSecrets.Contains(x) &&
                                         !renames.ContainsKey(x)))
                    values.TryAdd(variable, null);

            foreach (var variable in Variables)
                if (variable.Value.Length > 0) values[variable.Name] = variable.Value;
                else if (variable.ClearRequested) values[variable.Name] = null;

            await _service.SaveAsync(connector, _original?.Name);
            if (renames.Count > 0) await _service.RenameVariablesAsync(renames);
            await _service.SetVariablesAsync(values);

            if (OAuthClientSecret.Length > 0)
                await _service.SetOAuthClientSecretAsync(connector.Name, OAuthClientSecret);
            else if (ClearOAuthClientSecret || connector.Kind == McpConnectorKind.Local)
                await _service.SetOAuthClientSecretAsync(connector.Name, null);

            Saved = true;
            window?.Close();
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}

/// <summary>An environment variable or header of a connector.</summary>
public sealed class CopilotConnectorValueViewModel : ObservableObject
{
    private string _key;
    private string _value;
    private bool _isSecret;

    public CopilotConnectorValueViewModel(string key, string value, bool isSecret, bool hasStoredValue,
        string? storedKey)
    {
        _key = key;
        _value = value;
        _isSecret = isSecret;
        HasStoredValue = hasStoredValue;
        StoredKey = storedKey;
    }

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>Stores the value in the OS credential store instead of the Copilot CLI configuration.</summary>
    public bool IsSecret
    {
        get => _isSecret;
        set
        {
            if (!SetProperty(ref _isSecret, value)) return;
            OnPropertyChanged(nameof(Watermark));
            OnPropertyChanged(nameof(PasswordChar));
        }
    }

    public bool HasStoredValue { get; }

    /// <summary>Key the stored secret belongs to when the editor was opened.</summary>
    public string? StoredKey { get; }

    public char PasswordChar => IsSecret ? '•' : '\0';

    public string Watermark => IsSecret && HasStoredValue ? "Stored - leave empty to keep" :
        IsSecret ? "Secret value" : "Value, may use ${VAR}";

    public IRelayCommand? RemoveCommand { get; set; }
}

/// <summary>A <c>${VAR}</c> a connector references, with where its value comes from.</summary>
public sealed class CopilotConnectorVariableViewModel : ObservableObject
{
    private McpVariableSource? _source;
    private string _value = string.Empty;
    private bool _clearRequested;

    public CopilotConnectorVariableViewModel(string name, bool hasStoredValue)
    {
        Name = name;
        HasStoredValue = hasStoredValue;
        if (hasStoredValue) _source = McpVariableSource.OneWare;
        ClearCommand = new RelayCommand(() =>
        {
            ClearRequested = true;
            Value = string.Empty;
        });
    }

    public string Name { get; }

    public bool HasStoredValue { get; }

    public McpVariableSource? Source
    {
        get => _source;
        set
        {
            if (!SetProperty(ref _source, value)) return;
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(IsMissing));
        }
    }

    public bool IsMissing => Source == McpVariableSource.Missing && Value.Length == 0;

    public string SourceText => ClearRequested ? "Removed on save" : Source switch
    {
        McpVariableSource.OneWare => "Stored in OneWare",
        McpVariableSource.Environment => "From your environment",
        McpVariableSource.Missing => "Not set",
        _ => "Checking..."
    };

    public string Watermark => HasStoredValue && !ClearRequested ? "Stored - leave empty to keep" :
        Source == McpVariableSource.Environment ? "Optional, overrides the environment" : "Value";

    public string Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value)) return;
            if (value.Length > 0) ClearRequested = false;
            OnPropertyChanged(nameof(IsMissing));
        }
    }

    public bool ClearRequested
    {
        get => _clearRequested;
        private set
        {
            if (!SetProperty(ref _clearRequested, value)) return;
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(Watermark));
            OnPropertyChanged(nameof(CanClear));
        }
    }

    public bool CanClear => HasStoredValue && !ClearRequested;

    public IRelayCommand ClearCommand { get; }
}
