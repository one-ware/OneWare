using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.PackageManager.Models;

/// <summary>
/// Displays the state of a single step of a configuration profile import.
/// </summary>
public class ConfigurationImportStepModel : ObservableObject
{
    public ConfigurationImportStepModel(ConfigurationImportStep step, string title)
    {
        Step = step;
        Title = title;
    }

    public ConfigurationImportStep Step { get; }

    public string Title { get; }

    public ConfigurationImportStatus Status
    {
        get;
        set
        {
            if (!SetProperty(ref field, value)) return;

            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(ShowProgress));
        }
    } = ConfigurationImportStatus.Pending;

    public bool IsRunning => Status == ConfigurationImportStatus.Running;

    public bool IsCompleted => Status == ConfigurationImportStatus.Completed;

    /// <summary>
    /// What the step is currently doing, e.g. the package that is being installed.
    /// </summary>
    public string? Detail
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Progress of the running step in percent.
    /// </summary>
    public double ProgressPercent
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// True while the step reports a concrete progress value, false while it is indeterminate.
    /// </summary>
    public bool IsDeterminate
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool ShowProgress => IsRunning;

    public void Apply(ConfigurationImportProgress progress)
    {
        Status = progress.Status;
        Detail = progress.Detail;
        IsDeterminate = progress.Value.HasValue;
        ProgressPercent = Math.Clamp(progress.Value ?? 0, 0, 1) * 100;

        if (progress.Status == ConfigurationImportStatus.Completed) ProgressPercent = 100;
    }

    /// <summary>
    /// Puts the step back into its initial state, used when the import was aborted.
    /// </summary>
    public void Reset()
    {
        Status = ConfigurationImportStatus.Pending;
        Detail = null;
        IsDeterminate = false;
        ProgressPercent = 0;
    }
}
