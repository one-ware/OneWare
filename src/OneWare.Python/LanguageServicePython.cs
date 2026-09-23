using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Client;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Python;

public class LanguageServicePython : LanguageServiceLspAutoDownload
{
    private readonly PythonInterpreterService _interpreters;
    private readonly IWindowService _windows;
    private readonly ILogger _logger;
    private PythonInterpreterResolution _interpreter;
    private string? _configuredInterpreter;
    private string? _reportedProblem;
    private bool _launchRequested;

    public LanguageServicePython(string workspace, ISettingsService settingsService,
        IPackageService packageService, PythonInterpreterService interpreters, IWindowService windows, ILogger logger)
        : base(settingsService.GetSettingObservable<string>(PythonModule.LspPathSetting),
            PythonModule.PyreflyPackage, PythonModule.LspName, workspace, packageService,
            settingsService.GetSettingObservable<bool>("Experimental_AutoDownloadBinaries"), arguments: "lsp")
    {
        _interpreters = interpreters;
        _windows = windows;
        _logger = logger;
        _interpreter = interpreters.GetInterpreter(workspace);
        interpreters.InterpreterChanged += OnInterpreterChanged;
        LanguageServiceActivated += (_, _) => UpdateConfiguration();
    }

    public override Task ActivateAsync()
    {
        _launchRequested = true;
        return base.ActivateAsync();
    }

    public override Task DeactivateAsync()
    {
        _launchRequested = false;
        return base.DeactivateAsync();
    }

    protected override Task ActivateServerAsync()
    {
        _interpreter = _interpreters.GetInterpreter(Workspace!);
        ReportInterpreterProblem();
        return _interpreter.Status == PythonInterpreterStatus.InvalidExplicitSelection
            ? Task.CompletedTask
            : base.ActivateServerAsync();
    }

    protected override void ConfigureClientOptions(LanguageClientOptions options)
    {
        _configuredInterpreter = _interpreter.IsResolved ? _interpreter.ExecutablePath : null;
        options.WithInitializationOptions(PythonLanguageServerConfiguration.Create(_configuredInterpreter));
        // Each Python server owns one workspace, including its default/unscoped configuration.
        options.OnConfiguration(request => Task.FromResult(PythonLanguageServerConfiguration.Respond(request,
            PythonLanguageServerConfiguration.Create(_interpreter.IsResolved ? _interpreter.ExecutablePath : null))));
    }

    private void OnInterpreterChanged(object? sender, PythonInterpreterChangedEventArgs args)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(args.Workspace, PythonInterpreterService.NormalizeWorkspace(Workspace!), comparison)) return;
        Dispatcher.UIThread.Post(() =>
        {
            _interpreter = args.Resolution;
            UpdateConfiguration();
        });
    }

    private void UpdateConfiguration()
    {
        if (!_launchRequested) return;
        ReportInterpreterProblem();
        if (_interpreter.Status == PythonInterpreterStatus.InvalidExplicitSelection)
        {
            // Do not let Pyrefly silently choose another environment for an invalid explicit selection.
            if (IsActivated) _ = base.DeactivateAsync();
            return;
        }

        if (!IsActivated)
        {
            _ = RestartAsync();
            return;
        }
        if (!IsLanguageServiceReady) return;

        var interpreter = _interpreter.IsResolved ? _interpreter.ExecutablePath : null;
        if (_configuredInterpreter == interpreter) return;
        if (interpreter == null)
        {
            // Pyrefly does not clear pythonPath when a configuration response omits it.
            _ = RestartAsync();
            return;
        }
        _configuredInterpreter = interpreter;
        ReloadConfiguration();
    }

    private void ReportInterpreterProblem()
    {
        if (_interpreter.IsResolved)
        {
            _reportedProblem = null;
            return;
        }
        if (_reportedProblem == _interpreter.Message) return;
        var message = _interpreter.Message;
        _reportedProblem = message;
        _logger.Warning(message, showOutput: false);
        Dispatcher.UIThread.Post(() => _windows.ShowNotificationWithButton("Python interpreter",
            message, "Select interpreter",
            () => { _ = ContainerLocator.Current.Resolve<PythonInterpreterPickerService>().SelectInterpreterAsync(); },
            type: NotificationType.Warning));
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistancePython(editor, this);
    }
}