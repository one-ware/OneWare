using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Controls
{
    public class AdvancedHostWindow : HostWindow
    {
        private bool _cancelClose = true;
        private readonly IDockService _dockService;
        private readonly WindowHelper _windowHelper;

        public AdvancedHostWindow(IDockService dockService, WindowHelper windowHelper)
        {
//#if DEBUG
//            this.AttachDevTools();
//#endif
            _dockService = dockService;
            _windowHelper = windowHelper;
        }

        protected override Type StyleKeyOverride => typeof(HostWindow);

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (DataContext is IRootDock dock)
            {
                if (dock.VisibleDockables is { Count: > 0 } &&
                    dock.VisibleDockables[0] is DocumentDock tool)
                {
                    var docs = _dockService.OpenFiles
                        .Where(x => tool.VisibleDockables != null && tool.VisibleDockables.Contains(x.Value))
                        .ToArray();

                    var unsaved = docs
                        .Where(x => x.Value is { IsDirty: true })
                        .Select(x => x.Value)
                        .ToList();

                    if (unsaved.Any() && _cancelClose)
                    {
                        e.Cancel = true;
                        Activate();
                        _ = TrySaveFilesAsync(unsaved);
                    }
                    else
                    {
                        foreach (var i in docs)
                            _ = _dockService.CloseFileAsync(i.Key);
                    }
                }
            }
        }

        private async Task TrySaveFilesAsync(List<IExtendedDocument> unsavedFiles)
        {
            var close = await _windowHelper.HandleUnsavedFilesAsync(unsavedFiles, this);
            if (close)
            {
                _cancelClose = false;

                foreach (var file in unsavedFiles)
                {
                    if (file.CurrentFile != null)
                        _dockService.OpenFiles.Remove(file.CurrentFile);
                }

                Dispatcher.UIThread.Post(Close);
            }
        }
    }
}
