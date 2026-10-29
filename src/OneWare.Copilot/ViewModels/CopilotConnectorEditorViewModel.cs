using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Copilot.Connectors;
using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;

namespace OneWare.Copilot.ViewModels;

/// <summary>Adds or edits a connector. Values marked as secret are kept in the OS credential store.</summary>
public sealed class CopilotConnectorEditorViewModel : FlexibleWindowViewModelBase
{
    private readonly McpConnectorHub _hub;
    private readonly McpConnector? _original;
    private readonly IReadOnlyDictionary<string, string> _environment;
    private string _name = string.Empty;
    private bool _isRemote;
    private string _command = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _url = string.Empty;
    private bool _useSse;
    private string _oauthClientId = string.Empty;
    private string _oauthClientSecret = string.Empty;
    private bool _isPublicClient;
    private bool _isAdvancedExpanded;
    private bool _isPasteExpanded;
    private string _pasteText = string.Empty;
    private string? _pasteMessage;
    private string? _error;
    private bool _isBusy;
    private McpConnectorStatus? _testStatus;
    private string _missingVariables = string.Empty;

    private CopilotConnectorEditorViewModel(McpConnectorHub hub, McpConnector? original,
        IReadOnlyDictionary<string, string> environment)
    {
        _hub = hub;
        _original = original;
        _environment = environment;
        Title = original == null ? "Add MCP Server" : $"Edit MCP Server - {original.Name}";

        AddValueCommand = new RelayCommand(() =>
            AddValue(IsRemote ? Headers : Env, new McpConnectorValue(string.Empty, string.Empty, false)));
        ApplyPasteCommand = new RelayCommand(ApplyPaste);
        TestCommand = new AsyncRelayCommand(TestAsync);
        SaveCommand = new AsyncRelayCommand<FlexibleWindow?>(SaveAsync);
        CancelCommand = new RelayCommand<FlexibleWindow?>(x => x?.Close());

        // New servers start with the paste box open, which is the quickest way to add one from a README.
        _isPasteExpanded = original == null;
        Load(original ?? new McpConnector());
    }

    /// <param name="connector">The connector to edit, or <see langword="null" /> to add one.</param>
    public static async Task<CopilotConnectorEditorViewModel> CreateAsync(McpConnectorHub hub,
        McpConnector? connector)
    {
        var environment = await McpConnectorHub.GetRuntimeEnvironmentAsync();
        if (connector == null) return new CopilotConnectorEditorViewModel(hub, null, environment);

        var resolved = await hub.ResolveAsync(connector);
        var editor = new CopilotConnectorEditorViewModel(hub, resolved, environment);
        editor.OAuthClientSecret = (await hub.GetSecretsAsync(connector.Id)).OAuthClientSecret ?? string.Empty;
        editor.IsAdvancedExpanded = editor.HasAdvancedValues;
        return editor;
    }

    public bool IsNew => _original == null;

    public bool Saved { get; private set; }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsRemote
    {
        get => _isRemote;
        set
        {
            if (!SetProperty(ref _isRemote, value)) return;
            OnPropertyChanged(nameof(IsLocal));
            OnPropertyChanged(nameof(Values));
            OnPropertyChanged(nameof(ValuesTitle));
            OnPropertyChanged(nameof(AddValueText));
            UpdateVariables();
        }
    }

    public bool IsLocal
    {
        get => !IsRemote;
        set => IsRemote = !value;
    }

    public string Command
    {
        get => _command;
        set
        {
            if (SetProperty(ref _command, value)) UpdateVariables();
        }
    }

    /// <summary>Arguments, one per line.</summary>
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
        set => SetProperty(ref _oauthClientSecret, value);
    }

    public bool IsPublicClient
    {
        get => _isPublicClient;
        set => SetProperty(ref _isPublicClient, value);
    }

    public bool IsAdvancedExpanded
    {
        get => _isAdvancedExpanded;
        set => SetProperty(ref _isAdvancedExpanded, value);
    }

    public string RedirectUrl => McpConnectorHub.OAuthRedirectUrl;

    public bool IsPasteExpanded
    {
        get => _isPasteExpanded;
        set => SetProperty(ref _isPasteExpanded, value);
    }

    public string PasteText
    {
        get => _pasteText;
        set => SetProperty(ref _pasteText, value);
    }

    public string? PasteMessage
    {
        get => _pasteMessage;
        private set
        {
            if (SetProperty(ref _pasteMessage, value)) OnPropertyChanged(nameof(HasPasteMessage));
        }
    }

    public bool HasPasteMessage => !string.IsNullOrEmpty(PasteMessage);

    public ObservableCollection<CopilotConnectorValueViewModel> Env { get; } = [];

    public ObservableCollection<CopilotConnectorValueViewModel> Headers { get; } = [];

    /// <summary>Environment variables of local servers, headers of remote ones.</summary>
    public ObservableCollection<CopilotConnectorValueViewModel> Values => IsRemote ? Headers : Env;

    public string ValuesTitle => IsRemote ? "Headers" : "Environment variables";

    public string AddValueText => IsRemote ? "Add header" : "Add variable";

    /// <summary>Referenced <c>${VAR}</c>s that are not set in the environment OneWare starts servers with.</summary>
    public string MissingVariables
    {
        get => _missingVariables;
        private set
        {
            if (SetProperty(ref _missingVariables, value)) OnPropertyChanged(nameof(HasMissingVariables));
        }
    }

    public bool HasMissingVariables => MissingVariables.Length > 0;

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public McpConnectorStatus? TestStatus
    {
        get => _testStatus;
        private set
        {
            if (!SetProperty(ref _testStatus, value)) return;
            OnPropertyChanged(nameof(TestText));
            OnPropertyChanged(nameof(HasTestStatus));
            OnPropertyChanged(nameof(TestSucceeded));
            OnPropertyChanged(nameof(TestFailed));
        }
    }

    public bool HasTestStatus => TestStatus != null;

    public bool TestSucceeded => TestStatus?.State == McpConnectorState.Connected;

    public bool TestFailed => TestStatus is { State: not McpConnectorState.Connected };

    public string? TestText => TestStatus?.State switch
    {
        null => null,
        McpConnectorState.Connected => "Connected",
        McpConnectorState.NeedsSignIn =>
            "Reachable, sign-in required. Save and use Sign in to authorize OneWare.",
        _ => string.IsNullOrWhiteSpace(TestStatus.Message) ? "Failed to connect." : TestStatus.Message
    };

    public IRelayCommand AddValueCommand { get; }

    public IRelayCommand ApplyPasteCommand { get; }

    public IAsyncRelayCommand TestCommand { get; }

    public IAsyncRelayCommand<FlexibleWindow?> SaveCommand { get; }

    public IRelayCommand<FlexibleWindow?> CancelCommand { get; }

    private bool HasAdvancedValues => !string.IsNullOrWhiteSpace(OAuthClientId) || OAuthClientSecret.Length > 0 ||
                                      IsPublicClient || UseSse;

    private void Load(McpConnector connector)
    {
        Name = connector.Name;
        IsRemote = connector.Kind == McpConnectorKind.Remote;
        Command = connector.Command;
        Arguments = string.Join(Environment.NewLine, connector.Args);
        WorkingDirectory = connector.WorkingDirectory ?? string.Empty;
        Url = connector.Url;
        UseSse = connector.UseSse;
        OAuthClientId = connector.OAuthClientId ?? string.Empty;
        IsPublicClient = connector.OAuthPublicClient == true;

        Env.Clear();
        Headers.Clear();
        foreach (var value in connector.Env) AddValue(Env, value);
        foreach (var value in connector.Headers) AddValue(Headers, value);
        UpdateVariables();
    }

    private void AddValue(ObservableCollection<CopilotConnectorValueViewModel> target, McpConnectorValue value)
    {
        var row = new CopilotConnectorValueViewModel(value.Key, value.Value, value.IsSecret);
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

    private void ApplyPaste()
    {
        var warnings = new List<string>();
        var fallbackName = string.IsNullOrWhiteSpace(Name) ? "server" : Name.Trim();
        var servers = McpConnectorImport.ParseText(PasteText, warnings.Add, fallbackName);
        if (servers.Count == 0)
        {
            PasteMessage = warnings.Count > 0 ? string.Join(" ", warnings) : "No MCP server found.";
            return;
        }

        var imported = servers[0];
        // Editing keeps the identity (and so the stored secrets) of the connector.
        if (_original != null) imported.Connector.Id = _original.Id;
        Load(imported.Connector);
        if (imported.OAuthClientSecret != null) OAuthClientSecret = imported.OAuthClientSecret;
        if (HasAdvancedValues) IsAdvancedExpanded = true;

        if (servers.Count > 1)
            warnings.Insert(0, $"Found {servers.Count} servers, only '{imported.Connector.Name}' was used. " +
                               "Use Import to add all of them.");
        PasteMessage = warnings.Count > 0 ? string.Join(" ", warnings) : null;
        PasteText = string.Empty;
        IsPasteExpanded = warnings.Count > 0;
        TestStatus = null;
    }

    private McpConnector BuildConnector()
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
        connector.OAuthPublicClient = IsPublicClient ? true : _original?.OAuthPublicClient == false ? false : null;

        static List<McpConnectorValue> ToValues(IEnumerable<CopilotConnectorValueViewModel> rows) => rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => new McpConnectorValue(x.Key.Trim(), x.Value, x.IsSecret))
            .ToList();

        // Only the values of the selected kind are kept.
        connector.Env = IsRemote ? [] : ToValues(Env);
        connector.Headers = IsRemote ? ToValues(Headers) : [];
        return connector;
    }

    private void UpdateVariables()
    {
        var missing = BuildConnector().GetVariables().Where(x => !_environment.ContainsKey(x)).ToList();
        MissingVariables = missing.Count == 0
            ? string.Empty
            : $"{string.Join(", ", missing.Select(McpConnector.ToVariableReference))} " +
              (missing.Count == 1 ? "is" : "are") +
              " not set in your environment. Set it before starting OneWare, or mark the value as secret and enter it here.";
    }

    private string? Validate(McpConnector connector)
    {
        if (string.IsNullOrWhiteSpace(connector.Name)) return "Enter a name.";
        if (!McpConnector.IsValidName(connector.Name))
            return "The name needs a letter or digit and may only contain letters, digits, '.', '-' and '_'.";

        if (connector.Kind == McpConnectorKind.Local)
        {
            if (string.IsNullOrWhiteSpace(connector.Command)) return "Enter the command that starts the server.";
        }
        else if (!Uri.TryCreate(connector.Url, UriKind.Absolute, out var uri) ||
                 uri.Scheme is not ("http" or "https"))
        {
            return "Enter the server's http(s) URL.";
        }

        var keys = Values.Where(x => !string.IsNullOrWhiteSpace(x.Key)).Select(x => x.Key.Trim()).ToList();
        if (keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Count)
            return IsRemote ? "Each header may only be set once." : "Each variable may only be set once.";

        var missingSecret = Values.FirstOrDefault(x =>
            x.IsSecret && !string.IsNullOrWhiteSpace(x.Key) && x.Value.Length == 0);
        return missingSecret != null ? $"Enter the secret value of '{missingSecret.Key.Trim()}'." : null;
    }

    private async Task TestAsync()
    {
        var connector = BuildConnector();
        Error = Validate(connector);
        if (Error != null || IsBusy) return;

        IsBusy = true;
        TestStatus = new McpConnectorStatus(McpConnectorState.Pending);
        try
        {
            TestStatus = await _hub.CheckAsync(connector, report: false);
            if (McpConnectorHub.IsDynamicRegistrationError(TestStatus.Message)) IsAdvancedExpanded = true;
        }
        catch (Exception e)
        {
            TestStatus = new McpConnectorStatus(McpConnectorState.Failed, e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync(FlexibleWindow? window)
    {
        var connector = BuildConnector();
        Error = Validate(connector);
        if (Error != null) return;

        IsBusy = true;
        try
        {
            await _hub.SaveAsync(connector, OAuthClientSecret.Length > 0 ? OAuthClientSecret : null);
            Saved = true;
            window?.Close();
        }
        catch (Exception e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

/// <summary>An environment variable or header of a connector.</summary>
public sealed class CopilotConnectorValueViewModel : ObservableObject
{
    private string _key;
    private string _value;
    private bool _isSecret;
    private bool _isRevealed;

    public CopilotConnectorValueViewModel(string key, string value, bool isSecret)
    {
        _key = key;
        _value = value;
        _isSecret = isSecret;
        ToggleRevealCommand = new RelayCommand(() => IsRevealed = !IsRevealed);
    }

    public string Key
    {
        get => _key;
        set
        {
            if (!SetProperty(ref _key, value)) return;
            // Suggests storing credentials securely; never silently unmarks a value the user marked.
            if (!IsSecret && Value.Length == 0 && McpConnector.LooksSecret(value)) IsSecret = true;
        }
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    /// <summary>Stores the value in the OS credential store instead of mcp.json.</summary>
    public bool IsSecret
    {
        get => _isSecret;
        set
        {
            if (!SetProperty(ref _isSecret, value)) return;
            OnPropertyChanged(nameof(PasswordChar));
            OnPropertyChanged(nameof(Watermark));
            OnPropertyChanged(nameof(SecretToolTip));
        }
    }

    public bool IsRevealed
    {
        get => _isRevealed;
        set
        {
            if (SetProperty(ref _isRevealed, value)) OnPropertyChanged(nameof(PasswordChar));
        }
    }

    public char PasswordChar => IsSecret && !IsRevealed ? '•' : '\0';

    public string Watermark => IsSecret ? "Secret value" : "Value, may use ${VAR}";

    public string SecretToolTip => IsSecret
        ? "Stored in the OS credential store. Click to store it in mcp.json instead."
        : "Stored in mcp.json. Click to store it in the OS credential store instead.";

    public IRelayCommand ToggleRevealCommand { get; }

    public IRelayCommand? RemoveCommand { get; set; }
}
