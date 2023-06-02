using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Shared.Services;

namespace OneWare.Core.Views.Windows;

public class AdvancedHostWindow : HostWindow
{
    private bool _cancelClose = true;
    private IDockService _dockService;

    protected override Type StyleKeyOverride => typeof(HostWindow);

    public AdvancedHostWindow(IDockService dockService)
    {
#if DEBUG
        this.AttachDevTools();
#endif

        _dockService = dockService;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is IRootDock dock)
            if (dock.VisibleDockables is { Count: > 0 } &&
                dock.VisibleDockables[0] is DocumentDock tool)
            {
                var docs = _dockService.OpenFiles
                    .Where(x => tool.VisibleDockables != null && tool.VisibleDockables.Contains(x.Value)).ToArray();

                var unsaved = docs.Where(x => x.Value is EditViewModel { IsDirty: true })
                    .Select(x => x.Value)
                    .Cast<EditViewModel>()
                    .ToList();

                if (unsaved.Any() && _cancelClose)
                {
                    e.Cancel = true;
                    Activate();
                    _ = TrySafeFilesAsync(unsaved);
                }
                else
                {
                    foreach (var i in docs) _ = _dockService.CloseFileAsync(i.Key);
                }
            }
    }

    private async Task TrySafeFilesAsync(List<EditViewModel> unsavedFiles)
    {
        var close = await App.HandleUnsavedFilesAsync(unsavedFiles, this);
        if (close)
        {
            _cancelClose = false;
            Dispatcher.UIThread.Post(Close);
        }
    }
}