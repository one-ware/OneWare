using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public class ConfigurationProfileImportViewModel : FlexibleWindowViewModelBase
{
    private readonly IConfigurationProfileService _configurationProfileService;
    private readonly ILogger _logger;
    private readonly string _source;
    private readonly ConfigurationProfileSourceKind _sourceKind;

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _started;

    /// <param name="source">Path or url of the configuration profile, depending on <paramref name="sourceKind" />.</param>
    /// <param name="sourceKind">Defines how <paramref name="source" /> is resolved.</param>
    /// <param name="configurationProfileService">Service performing the import.</param>
    /// <param name="logger">Logger for import failures.</param>
    public ConfigurationProfileImportViewModel(string source, ConfigurationProfileSourceKind sourceKind,
        IConfigurationProfileService configurationProfileService, ILogger logger)
    {
        _source = source;
        _sourceKind = sourceKind;
        _configurationProfileService = configurationProfileService;
        _logger = logger;

        Title = "Import Configuration";
    }

    public ObservableCollection<ConfigurationImportStepModel> Steps { get; } =
    [
        new(ConfigurationImportStep.Settings, "Apply settings"),
        new(ConfigurationImportStep.Packages, "Install packages")
    ];

    public bool IsImporting
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? ErrorMessage
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public RelayCommand<FlexibleWindow> DoneCommand => new(window => window?.Close());

    public RelayCommand<FlexibleWindow> CancelCommand => new(window =>
    {
        if (IsImporting) _cancellationTokenSource?.Cancel();
        else window?.Close();
    });

    /// <summary>
    ///     Starts the import. Called once when the view is attached to the visual tree, so the
    ///     <see cref="Progress{T}" /> callbacks are marshalled back to the ui thread.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started) return;
        _started = true;

        IsImporting = true;
        IsDirty = true;
        ErrorMessage = null;

        _cancellationTokenSource = new CancellationTokenSource();
        var progress = new Progress<ConfigurationImportProgress>(Report);

        try
        {
            if (_sourceKind == ConfigurationProfileSourceKind.Url && !IsHttpUrl(_source))
                throw new InvalidOperationException($"'{_source}' is not a valid http(s) url.");
            
            var profile = _sourceKind == ConfigurationProfileSourceKind.File
                ? await _configurationProfileService.LoadFromFileAsync(_source, _cancellationTokenSource.Token)
                : await _configurationProfileService.LoadFromSourceAsync(_source, _cancellationTokenSource.Token);

            await _configurationProfileService.ImportAsync(profile, progress, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Import cancelled.";
            StopRunningSteps();
        }
        catch (Exception e)
        {
            _logger.Error($"Failed to import configuration profile from '{_source}': {e.Message}", e, false);
            ErrorMessage = e.Message;
            StopRunningSteps();
        }
        finally
        {
            IsImporting = false;
            IsDirty = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private void Report(ConfigurationImportProgress progress)
    {
        foreach (var step in Steps)
            if (step.Step == progress.Step)
                step.Apply(progress);
    }

    /// <summary>
    ///     An exception or a cancellation aborts the whole import, so the step that was running just
    ///     stops; the reason is shown in <see cref="ErrorMessage" />.
    /// </summary>
    private void StopRunningSteps()
    {
        foreach (var step in Steps)
            if (step.Status == ConfigurationImportStatus.Running)
                step.Reset();
    }
    
    private static bool IsHttpUrl(string source) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}