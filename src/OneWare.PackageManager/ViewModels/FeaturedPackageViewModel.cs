using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Enums;

namespace OneWare.PackageManager.ViewModels;

/// <summary>
///     Backs the hero banner above the package list. It promotes a single hardcoded package and is not
///     part of the list itself, so it can never move or reorder any package entry.
/// </summary>
public class FeaturedPackageViewModel : ObservableObject, IDisposable
{
    private PackageViewModel? _target;

    public FeaturedPackageViewModel(string title, string description, string iconResourceKey, string learnMoreUrl,
        ICommand showDetailsCommand)
    {
        Title = title;
        Description = description;
        IconResourceKey = iconResourceKey;
        LearnMoreUrl = learnMoreUrl;
        ShowDetailsCommand = showDetailsCommand;
        PrimaryCommand = new AsyncRelayCommand(ExecutePrimaryAsync);
    }

    public string Title { get; }

    public string Description { get; }

    public string IconResourceKey { get; }

    public string LearnMoreUrl { get; }

    public ICommand ShowDetailsCommand { get; }

    /// <summary>
    ///     Runs the action of the promoted package. Unlike the list row, the banner does not select the
    ///     package, so the tabs the install flow depends on have to be resolved explicitly.
    /// </summary>
    public AsyncRelayCommand PrimaryCommand { get; }

    /// <summary>
    ///     The view model of the promoted package, or null while the package is not part of the catalog.
    /// </summary>
    public PackageViewModel? Target
    {
        get => _target;
        set
        {
            if (ReferenceEquals(_target, value)) return;

            if (_target != null) _target.StatusChanged -= OnTargetStatusChanged;

            _target = value;

            if (_target != null) _target.StatusChanged += OnTargetStatusChanged;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    /// <summary>
    ///     The banner is only shown while the package is not on disk yet. <see cref="PackageStatus.Installing" />
    ///     keeps it visible so it does not disappear underneath the cancel button during a download.
    /// </summary>
    public bool IsVisible => _target?.PackageState.Status is PackageStatus.Available
        or PackageStatus.Installing
        or PackageStatus.UpdateAvailable
        or PackageStatus.UpdateAvailablePrerelease;

    public void Dispose()
    {
        if (_target != null) _target.StatusChanged -= OnTargetStatusChanged;
        _target = null;
    }

    private void OnTargetStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsVisible));
    }

    private async Task ExecutePrimaryAsync()
    {
        if (_target is not { } target) return;

        // Packages with AcceptLicenseBeforeDownload abort silently when the license tab is missing.
        if (!target.IsTabsResolved) await target.ResolveTabsAsync();

        // The status may have changed while the tabs were resolved, so the command is read again.
        if (target.MainButtonCommand is { } command && command.CanExecute(null))
            command.Execute(null);
    }
}
