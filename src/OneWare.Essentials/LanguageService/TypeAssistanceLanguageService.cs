using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Prism.Ioc;
using CompletionList = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionList;
using IFile = OneWare.Essentials.Models.IFile;
using InlayHint = OneWare.Essentials.EditorExtensions.InlayHint;
using Location = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.LanguageService
{
    /// <summary>
    /// Type Assistance that uses ILanguageService to perform assistance
    /// </summary>
    public abstract class TypeAssistanceLanguageService : TypeAssistanceBase
    {
        private readonly IBrush _highlightBackground = SolidColorBrush.Parse("#3300c8ff");

        private bool _completionBusy;
        private DispatcherTimer? _dispatcherTimer;
        private TimeSpan _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;

        private TimeSpan _lastEditTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastCaretChangeTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastDocumentChangedRefreshTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastCaretChangedRefreshTime = DateTime.Now.TimeOfDay;

        private static ISettingsService SettingsService => ContainerLocator.Container.Resolve<ISettingsService>();

        private readonly TimeSpan _timerTimeSpan = TimeSpan.FromMilliseconds(100);
        protected virtual TimeSpan DocumentChangedRefreshTime => TimeSpan.FromMilliseconds(500);
        protected virtual TimeSpan CaretChangedRefreshTime => TimeSpan.FromMilliseconds(100);

        protected ILanguageService Service { get; }

        protected TypeAssistanceLanguageService(IEditor evm, ILanguageService langService) : base(evm)
        {
            Service = langService;
        }

        #region Initialisation

        public override void Open()
        {
            base.Open();

            Service.LanguageServiceActivated += Server_Activated;
            Service.LanguageServiceDeactivated += Server_Deactivated;

            if (Service.IsLanguageServiceReady) Server_Activated(this, EventArgs.Empty);

            CodeBox.Document.Changed -= DocumentChanged;
            CodeBox.Document.Changed += DocumentChanged;
            Editor.FileSaved -= FileSaved;
            Editor.FileSaved += FileSaved;
        }

        public override void Attach()
        {
            _dispatcherTimer?.Stop();
            _dispatcherTimer = new DispatcherTimer
            {
                Interval = _timerTimeSpan
            };

            _dispatcherTimer.Tick += Timer_Tick;
            _dispatcherTimer.Start();
        }

        public override void Detach()
        {
            _dispatcherTimer?.Stop();
            base.Detach();
        }

        protected virtual void Timer_Tick(object? sender, EventArgs e)
        {
            if (_lastEditTime > _lastDocumentChangedRefreshTime && _lastEditTime <= DateTime.Now.TimeOfDay - DocumentChangedRefreshTime)
            {
                _lastDocumentChangedRefreshTime = DateTime.Now.TimeOfDay;
                
                //Execute slower actions after no edit was done for 500ms
                CodeUpdated();
            }
            
            if (_lastCaretChangeTime > _lastCaretChangedRefreshTime && _lastCaretChangeTime <= DateTime.Now.TimeOfDay - CaretChangedRefreshTime)
            {
                _lastCaretChangedRefreshTime = DateTime.Now.TimeOfDay;
                
                //Execute slower actions after the caret was not changed for 100ms
                _ = GetDocumentHighlightAsync();
                _ = UpdateInlayHintsAsync();
            }

            if (_lastCompletionItemChangedTime > _lastCompletionItemResolveTime &&
                _lastCompletionItemChangedTime <= DateTime.Now.TimeOfDay - CaretChangedRefreshTime)
            {
                _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;
                
                //Execute slower actions after the caret was not changed for 100ms
                _ = ResolveCompletionAsync();
            }
        }

        protected virtual void CodeUpdated()
        {
            Service.RefreshTextDocument(CurrentFile.FullPath, CodeBox.Text);
            _ = UpdateSemanticTokensAsync();
        }

        private void Server_Activated(object? sender, EventArgs e)
        {
            OnAssistanceActivated();
        }

        private void Server_Deactivated(object? sender, EventArgs e)
        {
            OnAssistanceDeactivated();
        }

        protected override void OnAssistanceActivated()
        {
            if (!IsOpen) return;

            base.OnAssistanceActivated();
            Service.DidOpenTextDocument(CurrentFile.FullPath, Editor.CurrentDocument.Text);

            _ = UpdateSemanticTokensAsync();
            _ = UpdateInlayHintsAsync();
        }

        protected override void OnAssistanceDeactivated()
        {
            if (!IsOpen) return;
            base.OnAssistanceDeactivated();
            Editor.Editor.ModificationService.ClearModification("caretHighlight");
            Editor.Editor.ModificationService.ClearModification("semanticTokens");
            Editor.Editor.InlayHintGenerator.ClearInlineHints();
        }

        protected virtual void DocumentChanged(object? sender, DocumentChangeEventArgs e)
        {
            if (!IsOpen || !Service.IsLanguageServiceReady) return;

            var c = ConvertChanges(e);
            var changes = new Container<TextDocumentContentChangeEvent>(c);
            Service.RefreshTextDocument(CurrentFile.FullPath, changes);

            _lastEditTime = DateTime.Now.TimeOfDay;
        }

        private void FileSaved(object? sender, EventArgs e)
        {
            if (Service.IsLanguageServiceReady)
                Service.DidSaveTextDocument(CurrentFile.FullPath, Editor.CurrentDocument.Text);
        }

        public override void Close()
        {
            CodeBox.Document.Changed -= DocumentChanged;
            Editor.FileSaved -= FileSaved;
            Service.LanguageServiceActivated -= Server_Activated;
            if (_dispatcherTimer != null)
            {
                _dispatcherTimer.Tick -= Timer_Tick;
                _dispatcherTimer.Stop();
            }

            base.Close();
            if (Service.IsLanguageServiceReady) Service.DidCloseTextDocument(CurrentFile.FullPath);
        }

        #endregion

        public override async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady) return null;

            var pos = CodeBox.Document.GetLocation(offset);

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFile)
                .OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine
                                         && pos.Line <= error.EndLine
                                         && pos.Column >= error.StartColumn
                                         && pos.Column <= error.EndColumn);
            var info = "";

            if (error != null) info += error.Description + "\n";

            var hover = await Service.RequestHoverAsync(CurrentFile.FullPath,
                new Position(pos.Line - 1, pos.Column - 1));
            if (hover != null)
            {
                if (hover.Contents.HasMarkedStrings)
                    info += hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) info += hover.Contents.MarkupContent?.Value;
            }

            return string.IsNullOrWhiteSpace(info) ? null : info;
        }

        public override async Task<List<MenuItemViewModel>?> GetQuickMenuAsync(int offset)
        {
            var menuItems = new List<MenuItemViewModel>();
            if (!Service.IsLanguageServiceReady || offset > CodeBox.Document.TextLength) return menuItems;
            var location = CodeBox.Document.GetLocation(offset);
            var pos = CodeBox.Document.GetPositionFromOffset(offset);

            //Quick Fixes
            var error = GetErrorAtLocation(location);
            if (error != null && error.Diagnostic != null)
            {
                var codeactions = await Service.RequestCodeActionAsync(CurrentFile.FullPath,
                    new Range
                    {
                        Start = new Position(error.StartLine - 1, error.StartColumn - 1 ?? 0),
                        End = new Position(error.EndLine - 1 ?? 0, error.EndColumn - 1 ?? 0)
                    }, error.Diagnostic);

                if (codeactions is not null && IsOpen)
                {
                    var quickfixes = new ObservableCollection<MenuItemViewModel>();
                    foreach (var ca in codeactions)
                    {
                        if (ca.IsCodeAction && ca.CodeAction != null)
                        {
                            if (ca.CodeAction.Command != null)
                                quickfixes.Add(new MenuItemViewModel(ca.CodeAction.Title)
                                {
                                    Header = ca.CodeAction.Title,
                                    Command = new RelayCommand<Command>(ExecuteCommand),
                                    CommandParameter = ca.CodeAction.Command
                                });
                            else if (ca.CodeAction.Edit != null)
                                quickfixes.Add(new MenuItemViewModel(ca.CodeAction.Title)
                                {
                                    Header = ca.CodeAction.Title,
                                    Command = new AsyncRelayCommand<WorkspaceEdit>(Service.ApplyWorkspaceEditAsync),
                                    CommandParameter = ca.CodeAction.Edit
                                });
                        }
                    }

                    if (quickfixes.Count > 0)
                        menuItems.Add(new MenuItemViewModel("Quick fix")
                        {
                            Header = "Quick fix...",
                            Items = quickfixes
                        });
                }
            }

            //Refactorings
            var prepareRefactor = await Service.PrepareRenameAsync(CurrentFile.FullPath, pos);
            if (prepareRefactor != null)
                menuItems.Add(new MenuItemViewModel("Rename")
                {
                    Header = "Rename...",
                    Command = new AsyncRelayCommand<RangeOrPlaceholderRange>(StartRenameSymbolAsync),
                    CommandParameter = prepareRefactor,
                    IconObservable = Application.Current?.GetResourceObservable("VsImageLib.Rename16X")
                });

            var definition = await Service.RequestDefinitionAsync(CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (definition != null && IsOpen)
                foreach (var i in definition)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel("GoToDefinition")
                        {
                            Header = "Go to Definition",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel("GoToDefinition")
                        {
                            Header = "Go to Definition",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var declaration = await Service.RequestDeclarationAsync(CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (declaration != null && IsOpen)
                foreach (var i in declaration)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel("GoToDeclaration")
                        {
                            Header = "Go to Declaration",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel("GoToDeclaration")
                        {
                            Header = "Go to Declaration",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var implementation = await Service.RequestImplementationAsync(CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (implementation != null && IsOpen)
                foreach (var i in implementation)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel("GoToImplementation")
                        {
                            Header = "Go to Implementation",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel("GoToImplementation")
                        {
                            Header = "Go to Implentation",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var typeDefinition = await Service.RequestImplementationAsync(CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (typeDefinition != null && IsOpen)
                foreach (var i in typeDefinition)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel("GoToDefinition")
                        {
                            Header = "Go to Type Definition",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel("GoToDefinition")
                        {
                            Header = "Go to Type Definition",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            return menuItems;
        }

        protected async Task StartRenameSymbolAsync(RangeOrPlaceholderRange? range)
        {
            if (range == null) return;

            if (range.IsRange && range.Range != null)
            {
                await Task.Delay(10);

                var sOff = CodeBox.Document.GetOffsetFromPosition(range.Range.Start) - 1;
                var eOff = CodeBox.Document.GetOffsetFromPosition(range.Range.End) - 1;

                if (sOff >= eOff) return;

                var initialValue = CodeBox.Text[sOff..eOff];

                var textInputWindow = new TextInputWindow(CodeBox.TextArea,
                    new TextViewPosition(range.Range.Start.Line + 1, range.Range.Start.Character + 1), initialValue)
                {
                    CompleteAction = x => _ = RenameSymbolAsync(range, x ?? "")
                };
                textInputWindow.Show();
            }
            else
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Placeholder Range renaming not supported yet!");
            }
        }

        private async Task RenameSymbolAsync(RangeOrPlaceholderRange range, string newName)
        {
            if (Regex.IsMatch(newName, @"\W"))
            {
                ContainerLocator.Container.Resolve<ILogger>()
                    ?.Error($"Can't rename symbol to {newName}! Only letters, numbers and underscore allowed!");
                return;
            }

            if (range.IsRange && range.Range != null)
            {
                var workspaceEdit = await Service.RequestRenameAsync(CurrentFile.FullPath, range.Range.Start, newName);
                if (workspaceEdit != null && IsOpen)
                    await Service.ApplyWorkspaceEditAsync(new ApplyWorkspaceEditParams
                        { Edit = workspaceEdit, Label = "Rename" });
            }
            else
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Placeholder Range renaming not supported yet!");
            }
        }

        public override async Task<Action?> GetActionOnControlWordAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady || offset > CodeBox.Document.TextLength) return null;
            var location = CodeBox.Document.GetLocation(offset);

            var definition = await Service.RequestDefinitionAsync(CurrentFile.FullPath,
                new Position(location.Line - 1, location.Column - 1));
            if (definition != null && IsOpen)
                if (definition.FirstOrDefault() is { } loc)
                {
                    if (loc.IsLocation && loc.Location != null) return () => _ = GoToLocationAsync(loc.Location);
                    if (loc.IsLocationLink && loc.LocationLink != null) return () => GoToLocation(loc.LocationLink);
                }

            return null;
        }

        protected virtual async Task GoToLocationAsync(Location? location)
        {
            if (location == null) return;

            var path = Path.GetFullPath(location.Uri.GetFileSystemPath());
            var file = ContainerLocator.Container.Resolve<IProjectExplorerService>().SearchFullPath(path) as IFile;
            file ??= ContainerLocator.Container.Resolve<IProjectExplorerService>().GetTemporaryFile(path);

            var dockable = await ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(file);
            if (dockable is IEditor evm)
            {
                var sOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.Start) - 1;
                var eOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.End) - 1;
                if (sOff >= 0 && eOff >= 0 && eOff > sOff) evm.Select(sOff, eOff - sOff);
            }
        }

        public virtual void GoToLocation(LocationLink? location)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Log("Location link not supported"); //TODO   
        }

        protected override async Task TextEnteredAsync(TextInputEventArgs args)
        {
            try
            {
                if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoFormatting"))
                    TextEnteredAutoFormat(args);

                if (!Service.IsLanguageServiceReady || args.Text == null) return;

                if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoCompletion") && !_completionBusy)
                {
                    var triggerChar = args.Text!;
                    var beforeTriggerChar = CodeBox.CaretOffset > 1 ? CodeBox.Text[CodeBox.CaretOffset - 2] : ' ';

                    var signatureHelpTrigger = Service.GetSignatureHelpTriggerChars();

                    if (signatureHelpTrigger.Contains(triggerChar)) //Function Parameter / Overload insight
                    {
                        Completion?.Close();
                        await ShowSignatureHelpAsync(SignatureHelpTriggerKind.TriggerCharacter, triggerChar, false,
                            null);
                    }

                    var completionTriggerChars = Service.GetCompletionTriggerChars();
                    if (completionTriggerChars.Contains(triggerChar))
                    {
                        _completionBusy = true;
                        await ShowCompletionAsync(CompletionTriggerKind.TriggerCharacter, triggerChar);
                    }
                    else if (CharBeforeNormalCompletion(beforeTriggerChar) && triggerChar.All(char.IsLetter))
                    {
                        _completionBusy = true;
                        await ShowCompletionAsync(CompletionTriggerKind.Invoked, triggerChar);
                    }

                    _completionBusy = false;
                }

                await base.TextEnteredAsync(args);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        protected virtual void TextEnteredAutoFormat(TextInputEventArgs e)
        {
            if (e.Text == null) return;
            if (e.Text.Contains('\n') && Service.IsLanguageServiceReady)
            {
                var startIndex = CodeBox.CaretOffset - 1;
                var endIndex = CodeBox.CaretOffset;
                var minIndex = CodeBox.Document.GetLineByOffset(startIndex).Offset;
                var maxIndex = CodeBox.Document.GetLineByOffset(endIndex).EndOffset;
                var newLine = TextUtilities.GetNewLineFromDocument(CodeBox.Document, CodeBox.TextArea.Caret.Line);

                if (endIndex >= CodeBox.Text.Length) return;
                while (startIndex > minIndex && CodeBox.Text[endIndex] != '\n' && CodeBox.Text[startIndex] != '{' &&
                       CodeBox.Text[startIndex] != '(') startIndex--;
                while (endIndex < maxIndex && CodeBox.Text[endIndex] != '\n' && CodeBox.Text[endIndex] != '}' &&
                       CodeBox.Text[endIndex] != ')') endIndex++;

                if (CodeBox.Text[startIndex] == '(' && CodeBox.Text[endIndex] == ')' ||
                    CodeBox.Text[startIndex] == '{' && CodeBox.Text[endIndex] == '}')
                {
                    var startLine = CodeBox.Document.GetLineByOffset(startIndex);
                    var caretLine = startLine.LineNumber + 1;
                    var replaceString = CodeBox.Text.Substring(startIndex, endIndex - startIndex + 1).Trim();
                    if (CodeBox.Document.GetText(startLine.Offset, startLine.Length).Trim().Length > 1)
                    {
                        replaceString = replaceString.Insert(0, newLine);
                        caretLine++;
                    }

                    replaceString =
                        replaceString.Insert(1 + newLine.Length, $" {newLine} "); // <-- empty char for indentation

                    CodeBox.Document.BeginUpdate();
                    CodeBox.Document.Replace(startIndex, endIndex - startIndex + 1, replaceString);
                    IndentationStrategy?.IndentLines(CodeBox.Document, startLine.LineNumber,
                        startLine.LineNumber + replaceString.Split('\n').Length - 1);
                    CodeBox.Document.EndUpdate();
                    CodeBox.CaretOffset = CodeBox.Document.GetLineByNumber(caretLine).EndOffset;
                }
            }
        }

        public override void CaretPositionChanged(int offset)
        {
            base.CaretPositionChanged(offset);

            _lastCaretChangeTime = DateTime.Now.TimeOfDay;
        }

        private async Task GetDocumentHighlightAsync()
        {
            var result = await Service.RequestDocumentHighlightAsync(CurrentFile.FullPath,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1));

            if (result is not null)
            {
                var segments = result.Select(x =>
                        x.Range.GenerateTextModification(Editor.CurrentDocument, null, _highlightBackground))
                    .ToArray();
                Editor.Editor.ModificationService.SetModification("caretHighlight", segments);
            }
            else
            {
                Editor.Editor.ModificationService.ClearModification("caretHighlight");
            }
        }

        protected virtual async Task UpdateSemanticTokensAsync()
        {
            var tokens = await Service.RequestSemanticTokensFullAsync(CurrentFile.FullPath);

            var languageManager = ContainerLocator.Container.Resolve<ILanguageManager>();

            if (tokens != null)
            {
                var segments = tokens.Select(x =>
                    {
                        var offset =
                            Editor.CurrentDocument.GetOffsetFromPosition(new Position(x.Line, x.StartCharacter)) - 1;

                        if (!languageManager.CurrentEditorThemeColors.TryGetValue(x.TokenType.ToString(), out var b))
                            return null;

                        return new TextModificationSegment(offset, offset + x.Length)
                        {
                            Foreground = b
                        };
                    }).Where(x => x is not null)
                    .Cast<TextModificationSegment>()
                    .ToArray();

                Editor.Editor.ModificationService.SetModification("semanticTokens", segments);
            }
            else
            {
                Editor.Editor.ModificationService.ClearModification("semanticTokens");
            }
        }
        
        protected virtual async Task UpdateInlayHintsAsync()
        {
            var inlayHintContainer = await Service.RequestInlayHintsAsync(CurrentFile.FullPath, new Range(0,0, CodeBox.Document.LineCount, 0));

            if (inlayHintContainer is not null)
            {
                Editor.Editor.InlayHintGenerator.SetInlineHints(inlayHintContainer.Select(x => new InlayHint()
                {
                    Offset = Editor.CurrentDocument.GetOffset(x.Position.Line+1, x.Position.Character+1),
                    Text = x.Label.ToString()
                }));
            }
            else
            {
                Editor.Editor.InlayHintGenerator.ClearInlineHints();
            }
        }

        protected virtual async Task ShowSignatureHelpAsync(SignatureHelpTriggerKind triggerKind, string? triggerChar,
            bool retrigger, SignatureHelp? activeSignatureHelp)
        {
            var signatureHelp = await Service.RequestSignatureHelpAsync(CurrentFile.FullPath,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1), triggerKind,
                triggerChar, retrigger, activeSignatureHelp);
            if (signatureHelp != null && IsOpen)
            {
                var overloadProvider = ConvertOverloadProvider(signatureHelp);

                OverloadInsight = new OverloadInsightWindow(CodeBox)
                {
                    Provider = overloadProvider,
                    PlacementGravity = PopupGravity.TopRight,
                    AdditionalOffset = new Vector(0, -(SettingsService.GetSettingValue<int>("Editor_FontSize") * 1.4))
                };

                OverloadInsight.SetValue(TextBlock.FontSizeProperty,
                    SettingsService.GetSettingValue<int>("Editor_FontSize"));
                if (overloadProvider.Count > 0) OverloadInsight.Show();
            }
        }

        private CompositeDisposable _completionDisposable = new();

        protected virtual async Task ShowCompletionAsync(CompletionTriggerKind triggerKind, string? triggerChar)
        {
            //Console.WriteLine($"Completion request kind: {triggerKind} char: {triggerChar}");
            var completionOffset = CodeBox.CaretOffset;

            var lspCompletionItems = await Service.RequestCompletionAsync(CurrentFile.FullPath,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1),
                triggerKind, triggerKind == CompletionTriggerKind.Invoked ? null : triggerChar);

            var customCompletionItems = await GetCustomCompletionItemsAsync();

            if ((lspCompletionItems is not null || customCompletionItems.Count > 0) && IsOpen)
            {
                Completion?.Close();
                _completionDisposable = new CompositeDisposable();

                Completion = new CompletionWindow(CodeBox)
                {
                    CloseWhenCaretAtBeginning = false,
                    AdditionalOffset = new Vector(0, 3),
                    MaxHeight = 225,
                    CloseAutomatically = true,
                    StartOffset = completionOffset,
                    EndOffset = completionOffset
                };

                Observable.FromEventPattern(Completion, nameof(Completion.Closed)).Take(1).Subscribe(x =>
                {
                    _completionDisposable.Dispose();
                }).DisposeWith(_completionDisposable);

                Completion.CompletionList.ListBox.WhenValueChanged(x => x.ItemsSource).Subscribe(x =>
                {
                    if (Completion.CompletionList.ListBox.Items.Count == 0)
                    {
                        Completion.Close();
                    }
                }).DisposeWith(_completionDisposable);

                Completion.CompletionList.CompletionData.AddRange(customCompletionItems);

                if (lspCompletionItems is not null)
                {
                    if (triggerKind is CompletionTriggerKind.TriggerCharacter && triggerChar != null)
                    {
                        completionOffset++;
                    }

                    Completion.CompletionList.CompletionData.AddRange(ConvertCompletionData(lspCompletionItems,
                        completionOffset));
                }

                //Calculate CompletionWindow width
                var length = 0;
                foreach (var data in Completion.CompletionList.CompletionData)
                    if (data.Content is string str && str.Length > length)
                        length = str.Length;
                var calculatedWith = length * SettingsService.GetSettingValue<int>("Editor_FontSize") + 50;

                Completion.Width = calculatedWith > 400 ? 500 : calculatedWith;

                if (Completion.CompletionList.CompletionData.Count > 0)
                {
                    Completion.Show();
                    if (triggerKind is not CompletionTriggerKind.TriggerCharacter)
                    {
                        Completion.CompletionList.SelectItem(triggerChar);
                    }
                }
            }
        }

        public virtual Task<List<CompletionData>> GetCustomCompletionItemsAsync()
        {
            return Task.FromResult(new List<CompletionData>());
        }

        public override IEnumerable<MenuItemViewModel> GetTypeAssistanceQuickOptions()
        {
            return new List<MenuItemViewModel>
            {
                new("RestartLs")
                {
                    Header = "Restart Language Server",
                    Command = new RelayCommand(() => _ = Service.RestartAsync()),
                    IconObservable = Application.Current!.GetResourceObservable("VsImageLib.RefreshGrey16X")
                }
            };
        }

        public virtual void ExecuteCommand(Command? cmd)
        {
            if (cmd == null) return;
            if (Service.IsLanguageServiceReady && IsOpen) _ = Service.ExecuteCommandAsync(cmd);
        }

        public virtual async Task ResolveCompletionAsync()
        {
            //Resolve selected item
            if (Service.IsLanguageServiceReady && Completion is
                {
                    IsOpen: true,
                    CompletionList: { SelectedItem: CompletionData { CompletionItemLsp: not null } selectedItem }
                })
            {
                var resolvedCi = await Service.ResolveCompletionItemAsync(selectedItem.CompletionItemLsp);
                if (resolvedCi != null && IsOpen && Completion.IsOpen)
                {
                    var cc = ConvertCompletionItem(resolvedCi, selectedItem.CompletionOffset);
                    var cindex = Completion.CompletionList.CompletionData.IndexOf(selectedItem);
                    if (cindex >= 0)
                    {
                        Completion.CompletionList.CompletionData.Remove(selectedItem);
                        Completion.CompletionList.CompletionData.Insert(cindex, cc);
                    }
                }
            }
        }

        protected virtual bool CharBeforeNormalCompletion(char c)
        {
            return char.IsWhiteSpace(c) ||
                   c is ';' or '#' or '(' or ':' or '+' or '-' or '=' or '*' or '/' or '&' or ',';
        }

        protected virtual OverloadProvider ConvertOverloadProvider(SignatureHelp signatureHelp)
        {
            var overloadOptions = new List<(string, string?)>();
            foreach (var s in signatureHelp.Signatures)
            {
                if (s.Parameters is null) continue;
                var m1 = "```cpp\n" + s.Label + "(";
                for (var i = 0; i < s.Parameters.Count(); i++)
                {
                    var p = s.Parameters.ElementAt(i);
                    if (s.ActiveParameter.HasValue && s.ActiveParameter.Value == i)
                    {
                        m1 += p.Label.Label;
                        if (i < s.Parameters.Count() - 1) m1 += ", ";
                    }

                    m1 += p.Label.Label;
                    if (i < s.Parameters.Count() - 1) m1 += ", ";
                }

                m1 += ")\n```";
                var m2 = string.Empty;
                if (s.Documentation != null && s.Documentation.HasMarkupContent)
                    m2 += s.Documentation.MarkupContent?.Value;
                if (s.Documentation != null && s.Documentation.HasString) m2 += s.Documentation.String;
                overloadOptions.Add((m1, m2.Length > 0 ? m2 : null));
            }

            return new OverloadProvider(overloadOptions)
            {
                SignatureHelp = signatureHelp
            };
        }

        protected virtual IEnumerable<ICompletionData> ConvertCompletionData(CompletionList list, int offset)
        {
            //Parse completionitem
            foreach (var comp in list.Items) yield return ConvertCompletionItem(comp, offset);
        }

        protected virtual ICompletionData ConvertCompletionItem(CompletionItem comp, int offset)
        {
            var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
                ? instanceIcon
                : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

            void AfterComplete()
            {
                _ = ShowSignatureHelpAsync(SignatureHelpTriggerKind.Invoked, null, false, null);
            }

            var description = comp.Documentation != null
                ? (comp.Documentation.MarkupContent != null
                    ? comp.Documentation.MarkupContent.Value
                    : comp.Documentation.String)
                : null;

            return new CompletionData(comp.InsertText ?? comp.FilterText ?? "", comp.Label, description, icon, 0,
                comp, offset, AfterComplete);
        }

        public ErrorListItem? GetErrorAtLocation(TextLocation location)
        {
            foreach (var error in ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFile))
                if (location.Line >= error.StartLine && location.Column >= error.StartColumn &&
                    (location.Line < error.EndLine ||
                     location.Line == error.EndLine && location.Column <= error.EndColumn))
                    return error;
            return null;
        }

        protected IEnumerable<TextDocumentContentChangeEvent> ConvertChanges(DocumentChangeEventArgs e)
        {
            var l = new List<TextDocumentContentChangeEvent>();
            var map = e.OffsetChangeMap;

            foreach (var c in map)
            {
                var location = CodeBox.Document.GetLocation(c.Offset);

                //calculate newlines
                var newlines = e.RemovedText.Text.Count(x => x == '\n');
                var lastIndexNewLine = e.RemovedText.Text.LastIndexOf('\n');
                var lengthAfterLastNewLine = lastIndexNewLine >= 0
                    ? c.RemovalLength - lastIndexNewLine
                    : location.Column + c.RemovalLength;

                var endLocation = new TextLocation(location.Line + newlines, lengthAfterLastNewLine);

                var docChange = new TextDocumentContentChangeEvent
                {
                    Range = new Range
                    {
                        Start = new Position(location.Line - 1, location.Column - 1),
                        End = new Position(endLocation.Line - 1, endLocation.Column - 1)
                    },
                    Text = e.InsertedText.Text,
                    RangeLength = c.RemovalLength
                };

                l.Add(docChange);
            }

            return l;
        }
    }
}