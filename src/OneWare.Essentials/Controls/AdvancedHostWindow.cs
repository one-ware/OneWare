using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Controls;

public class AdvancedHostWindow : HostWindow
{
    private readonly IMainDockService _mainDockService;
    private bool _cancelClose = true;
    private WindowState _lastWindowState = WindowState.Normal;

    public AdvancedHostWindow(IMainDockService mainDockService)
    {
#if DEBUG
        this.AttachDevTools();
#endif
        _mainDockService = mainDockService;

        KeyDown += (s, args) =>
        {
            if (args.Key == Key.F11)
            {
                if (WindowState == WindowState.FullScreen)
                {
                    WindowState = _lastWindowState;
                    Classes.Remove("FullScreen");
                }
                else
                {
                    _lastWindowState = WindowState;
                    WindowState = WindowState.FullScreen;
                    Classes.Add("FullScreen");
                }
            }
            else if (args.Key == Key.Escape && WindowState == WindowState.FullScreen)
            {
                WindowState = _lastWindowState;
                Classes.Remove("FullScreen");
            }
        };
    }

    protected override Type StyleKeyOverride => typeof(HostWindow);

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is IRootDock dock)
            if (dock.VisibleDockables is { Count: > 0 } &&
                dock.VisibleDockables[0] is DocumentDock tool)
            {
                var docs = _mainDockService.OpenFiles
                    .Where(x => tool.VisibleDockables != null && tool.VisibleDockables.Contains(x.Value)).ToArray();

                var unsaved = docs.Where(x => x.Value is { IsDirty: true })
                    .Select(x => x.Value)
                    .ToList();

                if (unsaved.Any() && _cancelClose)
                {
                    e.Cancel = true;
                    Activate();
                    _ = TrySafeFilesAsync(unsaved);
                }
                else
                {
                    foreach (var i in docs) _ = _mainDockService.CloseFileAsync(i.Key);
                }
            }
    }

    private async Task TrySafeFilesAsync(List<IExtendedDocument> unsavedFiles)
    {
        var close = await WindowHelper.HandleUnsavedFilesAsync(unsavedFiles, this);
        if (close)
        {
            _cancelClose = false;
            foreach (var file in unsavedFiles)
                if (file.CurrentFile != null)
                    _mainDockService.OpenFiles.Remove(file.CurrentFile);

            Dispatcher.UIThread.Post(Close);
        }
    }
}