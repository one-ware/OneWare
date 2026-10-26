using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Core.Services;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.ViewModels.DockViews;

public class EditViewModel : ExtendedDocument, IEditor, ISelfWatchingDocument
{
    private const string DetectExternalChangesKey = "Editor_DetectExternalChanges";
    private const string NotifyExternalChangesKey = "Editor_NotifyExternalChanges";
    private static readonly TimeSpan ExternalChangeDebounce = TimeSpan.FromMilliseconds(250);

    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(150, 175, 50, 50));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromArgb(150, 155, 155, 0));
    private readonly BackupService _backupService;
    private readonly IErrorService _errorService;
    private readonly ILanguageManager _languageManager;

    private readonly IMainDockService _mainDockService;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private CompositeDisposable _composite = new();

    private IEnumerable<ErrorListItem>? _diagnostics;

    private bool _detectExternalChanges;
    private bool _isClosed;
    private FileSystemWatcher? _fileWatcher;
    private string? _watchedPath;
    private DispatcherTimer? _externalChangeTimer;
    private bool _isApplyingExternalChange;
    private bool _externalChangePending;
    private DateTime? _declinedExternalWriteTime;
    private FileConflictViewModel? _conflictView;

    public EditViewModel(string fullPath, ILogger logger, IFileIconService fileIconService, ISettingsService settingsService,
        IMainDockService mainDockService, ILanguageManager languageManager, IWindowService windowService,
        IProjectExplorerService projectExplorerService, IErrorService errorService,
        BackupService backupService) : base(fullPath, fileIconService, projectExplorerService, mainDockService, windowService)
    {
        _settingsService = settingsService;
        _mainDockService = mainDockService;
        _windowService = windowService;
        _languageManager = languageManager;
        _errorService = errorService;
        _backupService = backupService;

        TopExtensions = windowService.GetUiExtensions("EditView_Top");
        BottomExtensions = windowService.GetUiExtensions("EditView_Bottom");

        Title = $"Loading {Path.GetFileName(fullPath)}";

        logger.Log("Initializing " + fullPath);

        Undo = new RelayCommand(() => Editor.Undo());
        Redo = new RelayCommand(() => Editor.Redo());

        _languageManager.LanguageSupportAdded += (_, s) =>
        {
            if (Path.GetExtension(FullPath) == s)
                Dispatcher.UIThread.Post(() => UpdateCurrentFile(FullPath));
        };

        this.WhenValueChanged(x => x.Diagnostics).Subscribe(x =>
        {
            List<ScrollInfoLine> scrollInfo = new();

            if (_diagnostics != null)
            {
                Editor.MarkerService.SetDiagnostics(_diagnostics);

                // Editor.ModificationService.SetModification("Errors", _diagnostics.Where(x => x.Type == ErrorType.Error).Select(b =>
                // {
                //     var off = b.GetOffset(Editor.Document);
                //     return new TextModificationService.TextModificationSegment(off.startOffset, off.endOffset)
                //     {
                //         Brush = _errorBrushText
                //     };
                // }).ToArray());

                var errorLines = _diagnostics
                    .Where(b => b.Type is ErrorType.Error)
                    .Select(c => c.StartLine)
                    .Distinct();

                var warningLines = _diagnostics
                    .Where(b => b.Type is ErrorType.Warning)
                    .Select(c => c.StartLine)
                    .Distinct();

                scrollInfo.AddRange(warningLines.Select(l => new ScrollInfoLine(l, WarningBrush)));
                scrollInfo.AddRange(errorLines.Select(l => new ScrollInfoLine(l, ErrorBrush)));
            }

            ScrollInfo.Refresh("ErrorContext", scrollInfo.ToArray());
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        });

        _errorService.ErrorRefresh += (sender, o) =>
        {
            if (o is string path && path.EqualPaths(FullPath))
                Diagnostics = _errorService.GetErrorsForFile(FullPath);
        };

        _settingsService.GetSettingObservable<bool>("Editor_UseSpaces").Subscribe(x =>
        {
            Editor.Options.ConvertTabsToSpaces = x;
        });

        _settingsService.GetSettingObservable<int>("Editor_IndentationSize").Subscribe(x =>
        {
            Editor.Options.IndentationSize = x;
        });

        _settingsService.GetSettingObservable<bool>(DetectExternalChangesKey).Subscribe(x =>
        {
            _detectExternalChanges = x;
            UpdateFileWatcher();
        });
    }

    public bool DisableEditViewEvents { get; private set; }

    public ITypeAssistance? TypeAssistance
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ScrollInfoContext ScrollInfo { get; } = new();

    public IEnumerable<ErrorListItem>? Diagnostics
    {
        get => _diagnostics;
        set => SetProperty(ref _diagnostics, value);
    }

    public ObservableCollection<OneWareUiExtension> TopExtensions { get; }
    public ObservableCollection<OneWareUiExtension> BottomExtensions { get; }

    public ExtendedTextEditor Editor { get; } = new();

    public TextDocument CurrentDocument => Editor.Document;

    public event EventHandler? FileSaved;

    protected override void UpdateCurrentFile(string? oldPath)
    {
        Reset();

        if (string.IsNullOrWhiteSpace(FullPath)) throw new NullReferenceException(nameof(FullPath));

        Diagnostics = _errorService.GetErrorsForFile(FullPath);

        _isClosed = false;
        _declinedExternalWriteTime = null;
        CloseConflictView();
        UpdateFileWatcher();

        async Task OnInitialized()
        {
            var result = await LoadAsync();

            var disableAfterSetting =
                (int)_settingsService.GetSettingValue<double>("TypeAssistance_DisableLargeFile_Min");
            DisableEditViewEvents = CurrentDocument.TextLength > disableAfterSetting;
            if (DisableEditViewEvents)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    .Warning("Some features are disabled for this large file to reduce performance loss");
            }
            else
            {
                var scope = _languageManager.GetTextMateScopeByExtension(Path.GetExtension(FullPath));
                if (scope != null)
                {
                    if (Editor.TextMateInstallation == null) Editor.InitTextmate(_languageManager.RegistryOptions);
                    Editor.TextMateInstallation?.SetGrammar(scope);
                    _languageManager.WhenValueChanged(x => x.CurrentEditorTheme).Subscribe(x =>
                    {
                        Editor.TextMateInstallation?.SetTheme(x);
                    }).DisposeWith(_composite);
                }
                else
                {
                    if (Editor.TextMateInstallation != null) Editor.RemoveTextmate();
                }

                Observable.FromEventPattern(
                        h => Editor.Document.TextChanged += h,
                        h => Editor.Document.TextChanged -= h)
                    .Subscribe(x =>
                    {
                        IsDirty = true;
                        _conflictView?.ScheduleRefresh();
                    }).DisposeWith(_composite);

                if (result) InitTypeAssistance();
            }
        }

        _ = OnInitialized();
    }

    private void InitTypeAssistance()
    {
        if (string.IsNullOrWhiteSpace(FullPath)) return;

        TypeAssistance = _languageManager.GetTypeAssistance(this);

        if (TypeAssistance != null)
        {
            Editor.SetEnableBreakpoints(TypeAssistance, FullPath);

            if (TypeAssistance.FoldingStrategy != null)
            {
                _settingsService.GetSettingObservable<bool>("Editor_UseFolding").Subscribe(x =>
                {
                    Editor.SetEnableFolding(x);
                    if (x) UpdateFolding();
                }).DisposeWith(_composite);

                Observable.FromEventPattern(
                        h => Editor.Document.LineCountChanged += h,
                        h => Editor.Document.LineCountChanged -= h)
                    .Subscribe(x => { UpdateFolding(); }).DisposeWith(_composite);
            }

            // Observable.FromEventPattern(
            //         h => TypeAssistance.AssistanceActivated += h,
            //         h => TypeAssistance.AssistanceActivated -= h)
            //     .Subscribe(x =>
            //     {
            //         
            //     });
            //
            Observable.FromEventPattern(
                    h => TypeAssistance.AssistanceDeactivated += h,
                    h => TypeAssistance.AssistanceDeactivated -= h)
                .Subscribe(x => { }).DisposeWith(_composite);

            TypeAssistance.Open();
        }
    }

    private void UpdateFolding()
    {
        if (_settingsService.GetSettingValue<bool>("Editor_UseFolding") && Editor.FoldingManager != null)
            TypeAssistance?.FoldingStrategy?.UpdateFoldings(Editor.FoldingManager, CurrentDocument);
    }

    public void AutoIndent()
    {
        TypeAssistance?.AutoIndent();
    }

    public void Format()
    {
        TypeAssistance?.Format();
    }

    public void Comment()
    {
        TypeAssistance?.Comment();
    }

    public void Uncomment()
    {
        TypeAssistance?.Uncomment();
    }

    #region Jump

    public override void GoToDiagnostic(ErrorListItem item)
    {
        var offset = item.GetOffset(CurrentDocument);
        Select(offset.startOffset, offset.endOffset - offset.startOffset);
    }

    public async Task<bool> WaitForEditorReadyAsync()
    {
        const int timeOut = 1000;
        var now = DateTime.Now.Millisecond;
        while (DateTime.Now.Millisecond - now < timeOut)
        {
            if (Editor is { IsInitialized: true }) return true;
            await Task.Delay(100);
        }

        return false;
    }

    public void JumpToLine(int lineNumber, bool select = true)
    {
        _ = JumpToLineAsync(lineNumber, select);
    }

    private async Task JumpToLineAsync(int lineNumber, bool select = true)
    {
        if (!await WaitForEditorReadyAsync()) return;
        await Task.Delay(100);
        if (lineNumber <= CurrentDocument.LineCount)
        {
            var line = CurrentDocument.GetLineByNumber(lineNumber);
            if (select) Editor.Select(line.Offset, line.Length);
            Editor.CaretOffset = line.Offset;
            Editor.TextArea.Caret.BringCaretToView(Editor.ViewportHeight / 3);
        }
    }

    public void Select(int offset, int length)
    {
        _ = SelectAsync(offset, length);
    }

    public override bool OnClose()
    {
        var r = base.OnClose();
        if (!r) return false;
        _composite.Dispose();
        _isClosed = true;
        StopFileWatcher();
        CloseConflictView();
        return true;
    }

    private async Task SelectAsync(int offset, int length)
    {
        if (!await WaitForEditorReadyAsync()) return;
        await Task.Delay(100);
        if (offset + length <= Editor.Text.Length)
        {
            Editor.Select(offset, length);
            Editor.CaretOffset = offset;
            Editor.TextArea.Caret.BringCaretToView(Editor.ViewportHeight / 2);
        }
    }

    #endregion

    #region LoadAndSave

    protected override void Reset()
    {
        Editor.SetEnableFolding(false);
        TypeAssistance?.Close();
        _composite.Dispose();
        _composite = new CompositeDisposable();
    }

    public override Task<bool> SaveAsync()
    {
        return SaveAsync(false);
    }

    /// <param name="overwriteExternalChanges">Skips the check for changes made outside of OneWare Studio.</param>
    public async Task<bool> SaveAsync(bool overwriteExternalChanges)
    {
        if (IsReadOnly || string.IsNullOrWhiteSpace(FullPath)) return true;

        if (!overwriteExternalChanges && !await ResolveSaveConflictAsync()) return false;

        try
        {
            await PlatformHelper.WriteTextFileAsync(FullPath, CurrentDocument.Text);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            return false;
        }

        ContainerLocator.Container.Resolve<ILogger>()
            ?.Log($"Saved {Path.GetFileName(FullPath)}!");

        IsDirty = false;
        LastSaveTime = GetLastWriteTimeOrNow(FullPath);
        _declinedExternalWriteTime = null;
        LoadingFailed = false;
        CloseConflictView();

        FileSaved?.Invoke(this, EventArgs.Empty);

        return true;
    }

    private async Task<bool> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(FullPath)) return false;

        IsLoading = true;

        var success = true;
        try
        {
            var (text, writeTime) = await ReadFileWithRetryAsync(FullPath);

            var doc = await Task.Run(() =>
            {
                var d = new TextDocument(text);
                d.SetOwnerThread(null);
                return d;
            });

            doc.SetOwnerThread(Thread.CurrentThread);

            Editor.Document = doc;

            LastSaveTime = writeTime;

            CurrentDocument.UndoStack.ClearAll();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Error($"Failed loading file {FullPath}", e, false);

            success = false;
        }

        IsLoading = false;
        LoadingFailed = !success;
        IsDirty = false;

        if (success) _ = _backupService.SearchForBackupAsync(FullPath);

        return success;
    }

    internal static async Task<(string Text, DateTime WriteTime)> ReadFileWithRetryAsync(string path)
    {
        const int attempts = 5;
        for (var attempt = 1;; attempt++)
            try
            {
                // Take the timestamp before reading so a write that happens while reading is detected later
                var writeTime = File.GetLastWriteTime(path);
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
                using var reader = new StreamReader(stream);
                var text = await reader.ReadToEndAsync();
                return (text, writeTime);
            }
            catch (IOException) when (attempt < attempts && File.Exists(path))
            {
                // Another program is still writing the file
                await Task.Delay(50 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts)
            {
                await Task.Delay(50 * attempt);
            }
    }

    private static DateTime GetLastWriteTimeOrNow(string path)
    {
        try
        {
            return File.Exists(path) ? File.GetLastWriteTime(path) : DateTime.Now;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    ///     Like VS Code: if the file on disk is newer and differs, ask to compare or overwrite before saving.
    /// </summary>
    /// <returns>True if saving may continue.</returns>
    private async Task<bool> ResolveSaveConflictAsync()
    {
        try
        {
            if (!File.Exists(FullPath)) return true;
            if (!LoadingFailed && File.GetLastWriteTime(FullPath) <= LastSaveTime) return true;

            var (diskText, _) = await ReadFileWithRetryAsync(FullPath);
            if (diskText == CurrentDocument.Text) return true;
        }
        catch
        {
            return true;
        }

        var compareButton = new MessageBoxButton
        {
            Text = "Compare", Role = MessageBoxButtonRole.Custom, Style = MessageBoxButtonStyle.Primary,
            IsDefault = true
        };
        var overwriteButton = new MessageBoxButton
        {
            Text = "Overwrite", Role = MessageBoxButtonRole.Yes, Style = MessageBoxButtonStyle.Danger
        };
        var cancelButton = new MessageBoxButton { Text = "Cancel", Role = MessageBoxButtonRole.Cancel };

        var result = await _windowService.ShowMessageBoxAsync(new MessageBoxRequest
        {
            Title = "File changed",
            Message =
                $"Failed to save '{Path.GetFileName(FullPath)}': The content of the file is newer.\n\n" +
                "Compare your version with the file contents or overwrite the file with your changes.",
            Icon = MessageBoxIcon.Warning,
            Buttons = [compareButton, overwriteButton, cancelButton]
        }, _mainDockService.GetWindowOwner(this));

        if (result.Button == overwriteButton) return true;
        if (result.Button == compareButton) ShowConflictView();
        return false;
    }

    private void ShowConflictView()
    {
        if (_conflictView is not { IsClosed: false }) _conflictView = new FileConflictViewModel(this);

        _mainDockService.Show(_conflictView, DockShowLocation.Document);
        _ = _conflictView.RefreshAsync();
    }

    private void CloseConflictView()
    {
        if (_conflictView is { IsClosed: false } view) _mainDockService.CloseDockable(view);
        _conflictView = null;
    }

    /// <summary>
    ///     Discards the unsaved changes and takes over the version on disk. Can be undone.
    /// </summary>
    public async Task RevertToDiskAsync()
    {
        if (string.IsNullOrWhiteSpace(FullPath) || !File.Exists(FullPath)) return;

        try
        {
            var (text, writeTime) = await ReadFileWithRetryAsync(FullPath);

            if (text != CurrentDocument.Text) ApplyExternalText(text);

            LastSaveTime = writeTime;
            _declinedExternalWriteTime = null;
            IsDirty = false;
            LoadingFailed = false;
            CloseConflictView();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error($"Could not revert {FullPath}", e);
        }
    }

    #endregion

    #region ExternalChanges

    private void UpdateFileWatcher()
    {
        var path = FullPath;
        var shouldWatch = _detectExternalChanges && !_isClosed && !string.IsNullOrWhiteSpace(path) &&
                          !OperatingSystem.IsBrowser();

        if (shouldWatch && _fileWatcher != null && path.EqualPaths(_watchedPath)) return;

        StopFileWatcher();
        if (!shouldWatch) return;

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return;

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                IncludeSubdirectories = false,
                Filter = Path.GetFileName(path)
            };

            watcher.Changed += OnWatchedFileChanged;
            watcher.Created += OnWatchedFileChanged;
            watcher.Renamed += OnWatchedFileChanged;
            watcher.EnableRaisingEvents = true;

            _fileWatcher = watcher;
            _watchedPath = path;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()
                ?.Warning($"Could not watch {path} for external changes", e, false);
        }
    }

    private void StopFileWatcher()
    {
        _externalChangeTimer?.Stop();

        if (_fileWatcher == null) return;

        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Dispose();
        _fileWatcher = null;
        _watchedPath = null;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (sender != _fileWatcher || _watchedPath == null || !e.FullPath.EqualPaths(_watchedPath)) return;
            ScheduleExternalReload();
        });
    }

    private void ScheduleExternalReload()
    {
        if (_isClosed) return;

        // Debounce, since editors often write a file in multiple steps (truncate, write, touch)
        _externalChangeTimer ??= new DispatcherTimer(ExternalChangeDebounce, DispatcherPriority.Background,
            (_, _) =>
            {
                _externalChangeTimer?.Stop();
                _ = ReloadFromDiskAsync();
            });

        _externalChangeTimer.Stop();
        _externalChangeTimer.Start();
    }

    private async Task ReloadFromDiskAsync()
    {
        if (_isClosed || string.IsNullOrWhiteSpace(FullPath)) return;

        if (IsLoading)
        {
            ScheduleExternalReload();
            return;
        }

        if (_isApplyingExternalChange)
        {
            _externalChangePending = true;
            return;
        }

        _isApplyingExternalChange = true;
        try
        {
            var path = FullPath;
            if (!File.Exists(path)) return;

            (string Text, DateTime WriteTime) disk;
            try
            {
                disk = await ReadFileWithRetryAsync(path);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Warning($"Could not reload {path} after an external change", e, false);
                return;
            }

            if (_isClosed || IsLoading || !path.EqualPaths(FullPath)) return;

            if (disk.Text == CurrentDocument.Text)
            {
                LastSaveTime = disk.WriteTime;
                IsDirty = false;
                LoadingFailed = false;
                CloseConflictView();
                return;
            }

            // Like VS Code: keep unsaved changes, the conflict is resolved when saving
            if (IsDirty)
            {
                _conflictView?.ScheduleRefresh();
                return;
            }

            if (_declinedExternalWriteTime == disk.WriteTime) return;

            if (_settingsService.GetSettingValue<bool>(NotifyExternalChangesKey))
            {
                var result = await _windowService.ShowYesNoAsync("File changed",
                    $"{Path.GetFileName(path)} has been changed outside of OneWare Studio.\n\nDo you want to reload it?",
                    MessageBoxIcon.Warning, _mainDockService.GetWindowOwner(this));

                if (_isClosed || IsDirty || !path.EqualPaths(FullPath)) return;

                if (result != MessageBoxStatus.Yes)
                {
                    // Keep the editor version and don't ask again for this disk version.
                    // Saving later still shows the conflict dialog.
                    _declinedExternalWriteTime = disk.WriteTime;
                    return;
                }
            }

            ApplyExternalText(disk.Text);

            LastSaveTime = disk.WriteTime;
            _declinedExternalWriteTime = null;
            IsDirty = false;
            LoadingFailed = false;
        }
        finally
        {
            _isApplyingExternalChange = false;

            if (_externalChangePending)
            {
                _externalChangePending = false;
                ScheduleExternalReload();
            }
        }
    }

    /// <summary>
    ///     Replaces only the changed range so caret, scroll position, folding and markers outside of it are kept.
    /// </summary>
    private void ApplyExternalText(string newText)
    {
        var document = CurrentDocument;
        var oldText = document.Text;

        var maxPrefix = Math.Min(oldText.Length, newText.Length);
        var prefix = 0;
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix]) prefix++;
        if (prefix > 0 && oldText[prefix - 1] == '\r') prefix--;

        var maxSuffix = Math.Min(oldText.Length, newText.Length) - prefix;
        var suffix = 0;
        while (suffix < maxSuffix &&
               oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            suffix++;
        if (suffix > 0 && oldText[oldText.Length - suffix] == '\n') suffix--;

        document.Replace(prefix, oldText.Length - prefix - suffix,
            newText.Substring(prefix, newText.Length - prefix - suffix));
    }

    #endregion
}
