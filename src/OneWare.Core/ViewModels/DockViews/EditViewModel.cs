using System.Collections;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using DynamicData.Binding;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.Windows;
using OneWare.ErrorList.ViewModels;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.Enums;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using TextMateSharp.Grammars;

namespace OneWare.Core.ViewModels.DockViews
{
    public class EditViewModel : Document, IExtendedDocument, IEditor, IWaitForContent
    {
        private readonly ILogger _logger;
        private readonly IDockService _dockService;
        private readonly ILanguageManager _languageManager;
        private readonly ISettingsService _settingsService;
        private readonly IErrorService _errorService;
        private readonly IWindowService _windowService;
        private readonly IProjectExplorerService _projectExplorerService;
        private readonly BackupService _backupService;

        private string _fullPath;

        [DataMember]
        public string FullPath
        {
            get => _fullPath;
            set
            {
                SetProperty(ref _fullPath, value);
                Id = $"Editor: {value}";
            }
        }

        private IFile? _currentFile;
        public IFile? CurrentFile
        {
            get => _currentFile;
            private set => SetProperty(ref _currentFile, value);
        }
        
        public ExtendedTextEditor Editor { get; } = new();

        public ITypeAssistance? TypeAssistance { get; private set; }

        public TextDocument CurrentDocument => Editor.Document;

        public IRelayCommand Undo { get; }

        public IRelayCommand Redo { get; }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private bool _loadingFailed;
        public bool LoadingFailed
        {
            get => _loadingFailed;
            set => SetProperty(ref _loadingFailed, value);
        }

        private bool _isReadOnly;

        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        private bool _isDirty;

        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        public ScrollInfoContext ScrollInfo { get; } = new();
        

        private IEnumerable<ErrorListItemModel>? _diagnostics;
        
        public IEnumerable<ErrorListItemModel>? Diagnostics
        {
            get => _diagnostics;
            set => SetProperty(ref _diagnostics, value);
        }

        public event EventHandler? FileSaved;
        
        static IBrush _errorBrushText = new SolidColorBrush(Color.FromArgb(255, 175, 50, 50));
        static IBrush _errorBrush = new SolidColorBrush(Color.FromArgb(150, 175, 50, 50));
        static IBrush _warningBrush = new SolidColorBrush(Color.FromArgb(150, 155, 155, 0));

        public EditViewModel(string fullPath, ILogger logger, ISettingsService settingsService,
            IDockService dockService, ILanguageManager languageManager, IWindowService windowService,
            IProjectExplorerService projectExplorerService, IErrorService errorService, BackupService backupService)
        {
            _fullPath = fullPath;
            
            _logger = logger;
            _settingsService = settingsService;
            _dockService = dockService;
            _windowService = windowService;
            _projectExplorerService = projectExplorerService;
            _languageManager = languageManager;
            _errorService = errorService;
            _backupService = backupService;
            
            Title = $"Loading {Path.GetFileName(fullPath)}";

            logger.Log("Initializing " + fullPath + "", ConsoleColor.DarkGray);
            
            Undo = new RelayCommand(() => Editor.Undo());
            Redo = new RelayCommand(() => Editor.Redo());

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

                    scrollInfo.AddRange(warningLines.Select(l => new ScrollInfoLine(l, _warningBrush)));
                    scrollInfo.AddRange(errorLines.Select(l => new ScrollInfoLine(l, _errorBrush)));
                }
                
                ScrollInfo.Refresh("ErrorContext", scrollInfo.ToArray());
                Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            });
        }

        public void InitializeContent()
        {
            if (string.IsNullOrWhiteSpace(FullPath))
            {
                IsLoading = false;
                LoadingFailed = true;
                return;
            }
            
            CurrentFile = _projectExplorerService.Search(FullPath) as IFile ?? new ExternalFile(FullPath);

            Title = CurrentFile is ExternalFile ? $"[{CurrentFile.Header}]" : CurrentFile.Header;

            _errorService.ErrorRefresh += (sender, o) =>
            {
                if(o == CurrentFile) Diagnostics = _errorService.GetErrorsForFile(CurrentFile);
            };
            Diagnostics = _errorService.GetErrorsForFile(CurrentFile);

            _dockService.OpenFiles.TryAdd(CurrentFile, this);
            
            async void OnLoaded()
            {
                var result = await LoadAsync();
                
                var scope = _languageManager.GetTextMateScopeByExtension(CurrentFile.Extension);
                if (scope != null)
                {
                    var textMateInstallation = Editor.InstallTextMate(_languageManager.RegistryOptions);
                    textMateInstallation.SetGrammar(scope);
                    _languageManager.WhenValueChanged(x => x.CurrentEditorTheme).Subscribe(x =>
                    {
                        textMateInstallation.SetTheme(x);
                    });
                }
                //Editor.SyntaxHighlighting = _languageManager.GetHighlighting(CurrentFile.Extension);
                
                if(result) InitLanguageService();
            }
            OnLoaded();
        }

        private void InitLanguageService()
        {
            if(CurrentFile == null) return;
            
            var service = _languageManager.GetLanguageService(CurrentFile);

            if (service != null)
            {
                TypeAssistance = service.GetTypeAssistance(this);

                if (TypeAssistance != null)
                {
                    if (_settingsService.GetSettingValue<bool>("Editor_UseFolding"))
                    {
                        Editor.SetFolding(true);
                        UpdateFolding();
                    }
                    
                    Observable.FromEventPattern(
                            h => TypeAssistance.AssistanceActivated += h,
                            h => TypeAssistance.AssistanceActivated -= h)
                        .Subscribe(x =>
                        {
                            
                        });

                    Observable.FromEventPattern(
                            h => TypeAssistance.AssistanceDeactivated += h,
                            h => TypeAssistance.AssistanceDeactivated -= h)
                        .Subscribe(x => {  });
                }

                if (TypeAssistance?.CanAddBreakPoints ?? false)
                {
                    // TODO Editor.TextArea.LeftMargins.Add(new BreakPointMargin(Editor, currentFile, Global.Breakpoints));
                }
                
                if (service is { IsActivated: false }) _ = service.ActivateAsync();
                
                TypeAssistance?.Open();
            }

            _settingsService.GetSettingObservable<bool>("Editor_UseFolding").Subscribe(x =>
            {
                //x = x && (TypeAssistance?.Service.IsLanguageServiceReady ?? false);
                Editor.SetFolding(x);
                if (x) UpdateFolding();
            });

            Observable.FromEventPattern(
                    h => Editor.Document.TextChanged += h,
                    h => Editor.Document.TextChanged -= h)
                .Subscribe(x => { IsDirty = true; });

            Observable.FromEventPattern(
                    h => Editor.Document.LineCountChanged += h,
                    h => Editor.Document.LineCountChanged -= h)
                .Subscribe(x => { UpdateFolding(); });
        }

        private void UpdateFolding()
        {
            if (_settingsService.GetSettingValue<bool>("Editor_UseFolding") && Editor.FoldingManager != null)
                TypeAssistance?.FoldingStrategy?.UpdateFoldings(Editor.FoldingManager, CurrentDocument);
        }

        #region Jump

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

        public override bool OnClose()
        {
            if (IsDirty)
            {
                if(CurrentFile != null) _ = _dockService.CloseFileAsync(CurrentFile);
                return false;
            }
            else
            {
                if(CurrentFile != null) _dockService.OpenFiles.Remove(CurrentFile);
            }

            Reset();
            return true;
        }

        private void Reset()
        {
            TypeAssistance?.Close();
            if (CurrentFile is ExternalFile) ContainerLocator.Container.Resolve<IErrorService>().Clear(CurrentFile);
        }

        public async Task<bool> TryCloseAsync()
        {
            if (!IsDirty) return true;

            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                "Do you want to save changes to the file " + CurrentFile?.Header + "?", MessageBoxIcon.Warning,
                _dockService.GetWindowOwner(this));

            if (result == MessageBoxStatus.Yes)
            {
                if (await SaveAsync()) return true;
            }
            else if (result == MessageBoxStatus.No)
            {
                IsDirty = false;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Saves
        /// </summary>
        /// <returns>If file could be saved</returns>
        public async Task<bool> SaveAsync()
        {
            if (IsReadOnly) return true;

            var success = await SaveFileAsync(CurrentFile!.FullPath, CurrentDocument.Text);

            if (success)
            {
                CurrentFile.LastSaveTime = DateTime.Now;

                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Log($"Saved {CurrentFile.Header}!", ConsoleColor.Green);
                IsDirty = false;

                //if (MainDock.OpenComparisons.ContainsKey(CurrentFile.FullPath))
                //    MainDock.SourceControl.Compare(CurrentFile.FullPath, false);
                //_ = MainDock.SourceControl.RefreshAsync(); TODO

                FileSaved?.Invoke(this, EventArgs.Empty);

                CurrentFile.LoadingFailed = false;
                return true;
            }

            return false;
        }

        public virtual async Task<bool> LoadAsync()
        {
            if (CurrentFile == null) return false;
            
            var result = await LoadFileAsync();

            CurrentDocument.UndoStack.ClearAll();

            IsLoading = false;
            
            if (!result.Item1)
            {
                CurrentFile.LoadingFailed = true;

                OnFileLoaded(false);
                
                return false;
            }
            else
            {
                CurrentFile.LastSaveTime = result.lastModified;
                if (CurrentFile.Extension is not (null or "" or ".py"))
                    CurrentDocument.Text = result.Item2.Replace("\t", "    ");
                else CurrentDocument.Text = result.Item2;
                
                CurrentFile.LoadingFailed = false;
                OnFileLoaded(true);
                return true;
            }
        }

        private void OnFileLoaded(bool status)
        {
            if (CurrentFile == null || !status) return;
            _ = _backupService.SearchForBackupAsync(CurrentFile);
            IsDirty = false;
        }
        
        private async Task<(bool, string, DateTime lastModified)> LoadFileAsync()
        {
            if(!File.Exists(CurrentFile!.FullPath)) return (false, "", DateTime.MinValue); 
            try
            {
                var stream = new FileStream(CurrentFile!.FullPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite);
                    
                var text = "";
                using (var reader = new StreamReader(stream))
                {
                    text = await reader.ReadToEndAsync();
                }

                stream.Close();
                return (true, text, File.GetLastWriteTime(CurrentFile!.FullPath));
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Error($"Failed loading file {CurrentFile.FullPath}", e, false);
                return (false, "", DateTime.MinValue); 
            }
        }

        /// <summary>
        ///     Saves file async
        /// </summary>
        private async Task<bool> SaveFileAsync(string path, string text)
        {
            try
            {
                await Tools.WriteTextFileAsync(path, text);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
                return false;
            }

            return true;
        }

        #endregion

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
    }
}