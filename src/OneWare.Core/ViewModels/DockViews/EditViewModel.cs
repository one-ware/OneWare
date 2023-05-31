using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using OneWare.Core.Services;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.LanguageService;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;
using TextMateSharp.Grammars;

namespace OneWare.Core.ViewModels.DockViews
{
    public class EditViewModel : Document, IDisposable, IExtendedDocument, IEditor
    {
        private readonly ILogger _logger;
        private readonly IDockService _dockService;
        private readonly ISettingsService _settingsService;
        private readonly IWindowService _windowService;
        private readonly BackupService _backupService;
        
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        public ExtendedTextEditor Editor { get; } = new();

        public ITypeAssistance? TypeAssistance { get; private set; }

        public TextDocument CurrentDocument => Editor.Document;
        
        [DataMember]
        public IFile CurrentFile { get; init; }
        
        public IRelayCommand Undo { get; }

        public IRelayCommand Redo { get; }

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

        private Dictionary<IBrush, int[]> _scrollInfo = new();

        public Dictionary<IBrush, int[]> ScrollInfo
        {
            get => _scrollInfo;
            set => SetProperty(ref _scrollInfo, value);
        }

        public event EventHandler? FileSaved;

        public EditViewModel(IFile currentFile, ILogger logger, ISettingsService settingsService, IDockService dockService, ILanguageManager languageManager, IWindowService windowService, BackupService backupService)
        {
            _logger = logger;
            _settingsService = settingsService;
            _dockService = dockService;
            _windowService = windowService;
            _backupService = backupService;
            
            logger.Log("Initializing " + currentFile.FullPath + "", ConsoleColor.DarkGray);
            
            Id = currentFile.FullPath;
            Title = currentFile is ExternalFile ? $"[{currentFile.Header}]" : currentFile.Header;
            CurrentFile = currentFile;
            
            var service = languageManager.GetLanguageService(currentFile);

            //Syntax Highlighting
            var syntaxTheme = _settingsService.GetSettingValue<ThemeName>("Editor_SyntaxTheme");
            var registryOptions = new RegistryOptions(syntaxTheme);
            var lang = service?.TextMateLanguage ?? registryOptions.GetLanguageByExtension(CurrentFile.Extension);
            if (lang != null)
            {
                var textMateInstallation = Editor.InstallTextMate(registryOptions);
            
                _settingsService.GetSettingObservable<ThemeName>("Editor_SyntaxTheme")
                    .Subscribe(
                        x =>
                        {
                            if(x != syntaxTheme) textMateInstallation.SetTheme(registryOptions.LoadTheme(x));
                            syntaxTheme = x;
                        });
                textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(lang.Id));
            }
            
            if (service != null)
            {
                TypeAssistance = service.GetTypeAssistance(this);

                if (TypeAssistance != null)
                {
                    Observable.FromEventPattern(
                            h => TypeAssistance.AssistanceActivated += h, 
                            h => TypeAssistance.AssistanceActivated -= h)
                        .Subscribe(x =>
                        {
                            if (settingsService.GetSettingValue<bool>("Editor_UseFolding"))
                            {
                                Editor.SetFolding(true);
                                UpdateFolding();
                            }
                        })
                        .DisposeWith(_disposables);

                    Observable.FromEventPattern(
                            h => TypeAssistance.AssistanceDeactivated += h, 
                            h => TypeAssistance.AssistanceDeactivated -= h)
                        .Subscribe(x =>
                        {
                            Editor.SetFolding(false);
                        })
                        .DisposeWith(_disposables);
                }
                if (TypeAssistance?.CanAddBreakPoints ?? false)
                {
                    // TODO Editor.TextArea.LeftMargins.Add(new BreakPointMargin(Editor, currentFile, Global.Breakpoints));
                }
                if(service is { IsActivated: false }) _ = service.ActivateAsync();
            }
                
            settingsService.GetSettingObservable<bool>("Editor_UseFolding").Subscribe(x =>
            {
                x = x && (TypeAssistance?.Service.IsLanguageServiceReady ?? false);
                Editor.SetFolding(x);
                if(x) UpdateFolding();
            }).DisposeWith(_disposables);
            
            Observable.FromEventPattern(
                    h => Editor.Document.TextChanged  += h, 
                    h => Editor.Document.TextChanged  -= h)
                .Subscribe(x =>
                {
                    IsDirty = true;
                })
                .DisposeWith(_disposables);
            
            Observable.FromEventPattern(
                    h => Editor.Document.LineCountChanged  += h, 
                    h => Editor.Document.LineCountChanged  -= h)
                .Subscribe(x =>
                {
                    UpdateFolding();
                })
                .DisposeWith(_disposables);

            Undo = new RelayCommand(() => Editor.Undo());
            Redo = new RelayCommand(() => Editor.Redo());
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
            if(!await WaitForEditorReadyAsync()) return;
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
            if(!await WaitForEditorReadyAsync()) return;
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
                _ = _dockService.CloseFileAsync(CurrentFile);
                return false;
            }
            else
            {
                _dockService.OpenFiles.Remove(CurrentFile);
            }

            TypeAssistance?.Close();
            if(CurrentFile is ExternalFile) ContainerLocator.Container.Resolve<IErrorService>().Clear(CurrentFile);
            return true;
        }
        
        public async Task<bool> TryCloseAsync()
        {
            if (!IsDirty) return true;

            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                "Do you want to save changes to the file " + CurrentFile.Header + "?", MessageBoxIcon.Warning, _dockService.GetWindowOwner(this));
            
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
            
            var success = await SaveFileAsync(CurrentFile.FullPath, CurrentDocument.Text);
            
            if (success)
            {
                CurrentFile.LastSaveTime = DateTime.Now;

                ContainerLocator.Container.Resolve<ILogger>()?.Log("Saved " + CurrentFile.Header + "!", ConsoleColor.Green);
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
            var result = await LoadFileAsync();
            
            if (CurrentFile.Extension is not (null or "" or ".py"))
                CurrentDocument.Text = result.Item2.Replace("\t", "    ");
            
            else CurrentDocument.Text = result.Item2;

            CurrentDocument.UndoStack.ClearAll();
            
            CurrentFile.LastSaveTime = result.lastModified;

            if (!result.Item1)
            {
                CurrentFile.LoadingFailed = true;

                OnFileLoaded(false);

                //Todo output error
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Failed loading " + CurrentFile.Header + "!");
                return false;
            }
            else
            {
                CurrentFile.LoadingFailed = false;
                OnFileLoaded(true);
                return true;
            }
        }

        private void OnFileLoaded(bool status)
        {
            _ = _backupService.SearchForBackupAsync(CurrentFile);
            IsDirty = false;
        }

        /// <summary>
        ///     Loads file async
        /// </summary>
        private Task<(bool, string, DateTime lastModified)> LoadFileAsync()
        {
            if (CurrentFile == null) throw new NullReferenceException(nameof(CurrentFile));
            return Task.Run(() =>
            {
                try
                {
                    var stream = new FileStream(CurrentFile.FullPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite);
                    var text = "";
                    using (var reader = new StreamReader(stream))
                    {
                        text = reader.ReadToEnd();
                    }

                    stream.Close();
                    return (true, text, File.GetLastWriteTime(CurrentFile.FullPath));
                }
                catch (Exception e)
                {
                    ContainerLocator.Container.Resolve<ILogger>()?.Error("Failed loading file " + CurrentFile.FullPath + "! " + e, e);
                    return (false, "", DateTime.MinValue);
                }
            });
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
        
        public void AutoFormat()
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

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}