using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.DockViews;

/// <summary>
///     Compares the file on disk (left) with the unsaved editor version (right) after a save conflict.
/// </summary>
public class FileConflictViewModel : Document, INoSerializeLayout
{
    private readonly EditViewModel _editor;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isRefreshing;
    private bool _refreshPending;

    public FileConflictViewModel(EditViewModel editor)
    {
        _editor = editor;
        Id = $"FileConflict: {editor.FullPath}";
        Title = $"{Path.GetFileName(editor.FullPath)} (on disk) ↔ (yours)";
        LanguageExtension = Path.GetExtension(editor.FullPath);

        _refreshTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, (_, _) =>
        {
            _refreshTimer?.Stop();
            _ = RefreshAsync();
        });
        _refreshTimer.Stop();

        OverwriteCommand = new AsyncRelayCommand(() => _editor.SaveAsync(true));
        UseFileVersionCommand = new AsyncRelayCommand(_editor.RevertToDiskAsync);
    }

    public string FileName => Path.GetFileName(_editor.FullPath);

    public string LanguageExtension { get; }

    public ICollection<ComparisonControlSection>? Chunks
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsClosed { get; private set; }

    public IAsyncRelayCommand OverwriteCommand { get; }

    public IAsyncRelayCommand UseFileVersionCommand { get; }

    public void ScheduleRefresh()
    {
        if (IsClosed) return;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    public async Task RefreshAsync()
    {
        if (IsClosed) return;

        if (_isRefreshing)
        {
            _refreshPending = true;
            return;
        }

        _isRefreshing = true;
        try
        {
            var path = _editor.FullPath;
            var mine = _editor.CurrentDocument.Text;
            var disk = File.Exists(path) ? (await EditViewModel.ReadFileWithRetryAsync(path)).Text : string.Empty;

            var chunks = await Task.Run(() => DiffHelper.BuildDiff(disk, mine));
            if (!IsClosed) Chunks = chunks;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Warning($"Could not compare {FileName}", e, false);
        }
        finally
        {
            _isRefreshing = false;
            if (_refreshPending)
            {
                _refreshPending = false;
                ScheduleRefresh();
            }
        }
    }

    public override bool OnClose()
    {
        IsClosed = true;
        _refreshTimer.Stop();
        return base.OnClose();
    }
}
