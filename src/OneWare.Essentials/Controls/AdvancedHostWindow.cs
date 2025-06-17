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
using System.Linq;
using System.Threading.Tasks;

namespace OneWare.Essentials.Controls
{
    public class AdvancedHostWindow : HostWindow
    {
        private bool _cancelClose = true;
        private readonly IDockService _dockService;
        private readonly WindowHelper _windowHelper;
        private WindowState _lastWindowState = WindowState.Normal;

        public AdvancedHostWindow(IDockService dockService, WindowHelper windowHelper)
        {
#if DEBUG
            this.AttachDevTools();
#endif
            _dockService = dockService;
            _windowHelper = windowHelper;

            this.KeyDown += (s, args) =>
            {
                if (args.Key == Key.F11)
                {
                    ToggleFullScreen();
                }
                else if (args.Key == Key.Escape && WindowState == WindowState.FullScreen)
                {
                    ExitFullScreen();
                }
            };
        }

        protected override Type StyleKeyOverride => typeof(HostWindow);

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is IRootDock dock && dock.VisibleDockables?.Count > 0 && dock.VisibleDockables[0] is DocumentDock tool)
            {
                var docs = _dockService.OpenFiles
                    .Where(x => x.Value != null && tool.VisibleDockables?.Contains(x.Value) == true)
                    .ToArray();

                var unsaved = docs
                    .Where(x => x.Value?.IsDirty == true)
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
                    foreach (var doc in docs)
                    {
                        if (doc.Key != null)
                        {
                            _ = _dockService.CloseFileAsync(doc.Key);
                        }
                    }
                }
            }
        }

        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.FullScreen)
            {
                ExitFullScreen();
            }
            else
            {
                EnterFullScreen();
            }
        }

        private void EnterFullScreen()
        {
            _lastWindowState = WindowState;
            WindowState = WindowState.FullScreen;
            Classes.Add("FullScreen");
        }

        private void ExitFullScreen()
        {
            WindowState = _lastWindowState;
            Classes.Remove("FullScreen");
        }

        private async Task TrySafeFilesAsync(List<IExtendedDocument> unsavedFiles)
        {
            var close = await _windowHelper.HandleUnsavedFilesAsync(unsavedFiles, this);
            if (close)
            {
                _cancelClose = false;
                foreach (var file in unsavedFiles)
                {
                    if (file.CurrentFile != null)
                    {
                        _dockService.OpenFiles.Remove(file.CurrentFile);
                    }
                }

                await Dispatcher.UIThread.InvokeAsync(Close);
            }
        }
    }
}
