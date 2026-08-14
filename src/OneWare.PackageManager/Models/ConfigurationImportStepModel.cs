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
        }
    } = ConfigurationImportStatus.Pending;

    public bool IsRunning => Status == ConfigurationImportStatus.Running;

    public bool IsCompleted => Status == ConfigurationImportStatus.Completed;

    public void Apply(ConfigurationImportProgress progress)
    {
        Status = progress.Status;
    }
}
