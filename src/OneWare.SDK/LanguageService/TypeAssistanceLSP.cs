using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OneWare.SDK.EditorExtensions;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;
using OneWare.SDK.Extensions;
using Prism.Ioc;
using CompletionList = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionList;
using IFile = OneWare.SDK.Models.IFile;
using Location = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.SDK.LanguageService
{
    public abstract class TypeAssistanceLsp : TypeAssistance
    {
        private bool _completionBusy;
        private int _completionOffset;
        private DispatcherTimer? _dispatcherTimer;
        private TimeSpan _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;

        private TimeSpan _lastEditTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastRefreshTime = DateTime.Now.TimeOfDay;

        private ICompletionData? _lastSelectedCompletionItem;
        private static ISettingsService SettingsService => ContainerLocator.Container.Resolve<ISettingsService>();
        public LanguageServiceBase Service { get; }
        
        private readonly TimeSpan _timerTimeSpan = TimeSpan.FromMilliseconds(200);
        protected virtual TimeSpan RefreshTime => TimeSpan.FromMilliseconds(500);
        
        public TypeAssistanceLsp(IEditor evm, LanguageServiceBase langService) : base(evm)
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
        
        public override void Attach(CompletionWindow completion)
        {
            base.Attach(completion);
            
            completion.CompletionList.SelectionChanged += (_, _) =>
            {
                _lastSelectedCompletionItem = completion.CompletionList.SelectedItem;
                _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;
            };

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
            if (_lastEditTime > _lastRefreshTime && _lastEditTime <= DateTime.Now.TimeOfDay - RefreshTime)
            {
                _lastRefreshTime = DateTime.Now.TimeOfDay;
                CodeUpdated();
            }

            if (_lastCompletionItemChangedTime > _lastCompletionItemResolveTime &&
                _lastCompletionItemChangedTime <= DateTime.Now.TimeOfDay - RefreshTime)
            {
                _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;
                _ = ResolveCompletionAsync();
            }
        }

        public virtual void CodeUpdated()
        {
            if (Service.Client?.ServerSettings.Capabilities.TextDocumentSync?.Kind is TextDocumentSyncKind.Full)
            {
                Service.RefreshTextDocument(CurrentFile.FullPath, CodeBox.Text);
            }
            _ = UpdateSymbolsAsync();
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

            _ = UpdateSymbolsAsync();
        }

        protected override void OnAssistanceDeactivated()
        {
            if (!IsOpen) return;
            base.OnAssistanceDeactivated();
        }

        protected virtual void DocumentChanged(object? sender, DocumentChangeEventArgs e)
        {
            if (!IsOpen || !Service.IsLanguageServiceReady) return;

            if (Service.Client?.ServerSettings.Capabilities.TextDocumentSync?.Kind is TextDocumentSyncKind
                    .Incremental or TextDocumentSyncKind.None)
            {
                var c = ConvertChanges(e);
                var changes = new Container<TextDocumentContentChangeEvent>(c);
                Service.RefreshTextDocument(CurrentFile.FullPath, changes);
            }

            _lastEditTime = DateTime.Now.TimeOfDay;
        }

        private void FileSaved(object? sender, EventArgs e)
        {
            if (Service.IsLanguageServiceReady) Service.DidSaveTextDocument(CurrentFile.FullPath, Editor.CurrentDocument.Text);
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

            var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFile).OrderBy(x => x.Type)
                .FirstOrDefault(error => pos.Line >= error.StartLine 
                                         && pos.Line <= error.EndLine 
                                         && pos.Column >= error.StartColumn
                                         && pos.Column <= error.EndColumn);
            var info = "";
            
            if(error != null) info += error.Description + "\n";
            
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
                    var quickfixes = new List<IMenuItem>();
                    foreach (var ca in codeactions)
                    {
                        if (ca.IsCodeAction && ca.CodeAction != null)
                        {
                            if(ca.CodeAction.Command != null) quickfixes.Add(new MenuItemViewModel(ca.CodeAction.Title)
                            {
                                Header = ca.CodeAction.Title,
                                Command = new RelayCommand<Command>(ExecuteCommand),
                                CommandParameter = ca.CodeAction.Command
                            });
                            else if(ca.CodeAction.Edit != null) quickfixes.Add(new MenuItemViewModel(ca.CodeAction.Title)
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
                    ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.Rename16X") 
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

                TextInput = new TextInputWindow(CodeBox.TextArea,
                    new TextViewPosition(range.Range.Start.Line + 1, range.Range.Start.Character + 1), initialValue)
                {
                    CompleteAction = x => _ = RenameSymbolAsync(range, x ?? "")
                };
                TextInput.Show();
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
                ContainerLocator.Container.Resolve<ILogger>()?.Error($"Can't rename symbol to {newName}! Only letters, numbers and underscore allowed!");
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
            var file = ContainerLocator.Container.Resolve<IProjectExplorerService>().Search(path) as IFile;
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

        public override async Task TextEnteredAsync(TextInputEventArgs args)
        {
            try
            {
                if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoFormatting")) TypeAssistance(args);

                if (!Service.IsLanguageServiceReady || args.Text == null) return;

                var t = args.Text.Length > 0 ? args.Text[0] : ';';
                var b = CodeBox.CaretOffset > 1 ? CodeBox.Text[CodeBox.CaretOffset - 2] : ' ';
                //var cLine = CodeBox.Document.GetText(CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset));

                if (t == '(' || b == '(' ||
                    t == ',') //Function Parameter / Overload insight
                    if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoCompletion") &&
                        !_completionBusy)
                    {
                        Completion?.Close();
                        await ShowOverloadProviderAsync();
                    }

                if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoCompletion") && !_completionBusy &&
                    (CharBeforeNormalCompletion(b) && CharAtNormalCompletion(t) || //Normal completion
                     (CharAtNormalCompletion(b) || b is ')') && t == '.')) //Child insight
                {
                    _completionBusy = true;
                    _completionOffset = CodeBox.CaretOffset;
                    if (t == '.') _completionOffset++;
                    await ShowCompletionAsync(args.Text, t == '.' ? CompletionTriggerKind.Invoked : CompletionTriggerKind.TriggerCharacter);
                    _completionBusy = false;
                }

                await base.TextEnteredAsync(args);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
        }

        public virtual void TypeAssistance(TextInputEventArgs e)
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

                    replaceString = replaceString.Insert(1 + newLine.Length, $" {newLine} "); // <-- empty char for indentation

                    CodeBox.Document.BeginUpdate();
                    CodeBox.Document.Replace(startIndex, endIndex - startIndex + 1, replaceString);
                    IndentationStrategy?.IndentLines(CodeBox.Document, startLine.LineNumber,
                        startLine.LineNumber + replaceString.Split('\n').Length - 1);
                    CodeBox.Document.EndUpdate();
                    CodeBox.CaretOffset = CodeBox.Document.GetLineByNumber(caretLine).EndOffset;
                }
            }
        }

        public virtual async Task UpdateSymbolsAsync()
        {
            if (CodeBox.SyntaxHighlighting == null) return;

            if (Service.IsLanguageServiceReady)
            {
                var highlights = await RetrieveSymbolsAsync();
                
                if (highlights is not null)
                {
                    SetCustomColor(SymbolKind.Function, highlights);
                    SetCustomColor(SymbolKind.Class, highlights);
                }
                else
                {
                    //CustomHighlightManager?.ClearHighlights();
                }
            }
            else
            {
                //CustomHighlightManager?.ClearHighlights();
            }

            CodeBox.TextArea.TextView.Redraw();
        }

        public virtual async Task<List<(string, SymbolKind)>?> RetrieveSymbolsAsync()
        {
            var symbolInfo = await Service.RequestSymbolsAsync(CurrentFile.FullPath);

            if (symbolInfo is null) return null;
            
            var highlights = new List<(string, SymbolKind)>();
            if (IsOpen)
                foreach (var c in symbolInfo)
                    if (c.IsDocumentSymbol && c.DocumentSymbol != null)
                        //ContainerLocator.Container.Resolve<ILogger>()?.Log("DocumentSymbol not supported! TypeAssistanceLSP RetrieveSymbols()", ConsoleColor.Red);
                        highlights.Add((c.DocumentSymbol.Name, c.DocumentSymbol.Kind));
                    else if(c.IsDocumentSymbolInformation && c.SymbolInformation != null)
                        highlights.Add((c.SymbolInformation.Name, c.SymbolInformation.Kind));

            return highlights;
        }

        protected virtual void SetCustomColor(SymbolKind kind, IEnumerable<(string, SymbolKind)> symbols)
        {
            var color = CodeBox.SyntaxHighlighting.GetNamedColor(kind.ToString());

            if (color == null) return;

            var highlights = symbols
                .Where(x => x.Item2 == kind)
                .Select(x => x.Item1)
                .ToArray();

            //CustomHighlightManager?.SetHightlights(highlights, color);
        }

        protected virtual async Task ShowOverloadProviderAsync()
        {
            var signatureHelp = await Service.RequestSignatureHelpAsync(CurrentFile.FullPath,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1));
            if (signatureHelp != null && IsOpen)
            {
                OverloadInsight = new OverloadInsightWindow(CodeBox);

                var overloadProvider = ConvertOverloadProvider(signatureHelp);
                OverloadInsight.Provider = overloadProvider;

                OverloadInsight.PlacementGravity = PopupGravity.TopRight;
                OverloadInsight.AdditionalOffset = new Vector(0, -(SettingsService.GetSettingValue<int>("Editor_FontSize") * 1.4));
                OverloadInsight.SetValue(TextBlock.FontSizeProperty, SettingsService.GetSettingValue<int>("Editor_FontSize"));
                if (overloadProvider.Count > 0) OverloadInsight.Show();
            }
        }

        protected virtual async Task ShowCompletionAsync(string triggerChar, CompletionTriggerKind triggerKind)
        {
            //var t = triggerChar.Length > 0 ? triggerChar[0] : ';';

            var completion = await Service.RequestCompletionAsync(CurrentFile.FullPath,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1), triggerChar,
                triggerKind);
            var custom = await GetCustomCompletionItemsAsync();

            if ((completion is not null || custom.Count > 0) && IsOpen && Completion != null)
            {
                Completion.EndOffset = CodeBox.CaretOffset;
                Completion.StartOffset = CodeBox.CaretOffset;
                if (triggerKind is CompletionTriggerKind.TriggerCharacter) Completion.StartOffset -= triggerChar.Length;
                Completion.CompletionList.Reset();
                Completion.CompletionList.CompletionData.AddRange(custom);
                if (completion is not null)
                    Completion.CompletionList.CompletionData.AddRange(ConvertCompletionData(completion));

                //Calculate completionwindow width
                var length = 0;
                foreach (var dataprop in Completion.CompletionList.CompletionData)
                    if (dataprop.Content is string str && str.Length > length)
                        length = str.Length;
                var calwidth = length * SettingsService.GetSettingValue<int>("Editor_FontSize") + 50;

                Completion.Width = calwidth > 400 ? 500 : calwidth;
                if (Completion.CompletionList.CompletionData.Count > 0)
                {
                    if (triggerChar.Length == 1 && char.IsLetter(triggerChar[0]))
                        Completion.Show(triggerChar);
                    else Completion.Show();
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
                    ImageIconObservable = Application.Current?.GetResourceObservable("VsImageLib.RefreshGrey16X") 
                }
            };
        }

        public virtual void ExecuteCommand(Command? cmd)
        {
            if(cmd == null) return;
            if (Service.IsLanguageServiceReady && IsOpen) _ = Service.ExecuteCommandAsync(cmd);
        }

        public virtual async Task ResolveCompletionAsync()
        {
            //Resolve selected item
            if (Service.IsLanguageServiceReady && Completion != null && Completion.IsOpen &&
                _lastSelectedCompletionItem is CompletionData completionLsp && completionLsp.CompletionItemLsp != null)
            {
                var resolvedCi = await Service.ResolveCompletionItemAsync(completionLsp.CompletionItemLsp);
                if (resolvedCi != null && IsOpen && Completion.IsOpen)
                {
                    var cc = ConvertCompletionItem(resolvedCi, _completionOffset);
                    var cindex = Completion.CompletionList.CompletionData.IndexOf(completionLsp);
                    if (cindex >= 0)
                    {
                        Completion.CompletionList.CompletionData.Remove(completionLsp);
                        Completion.CompletionList.CompletionData.Insert(cindex, cc);
                    }
                }
            }
        }

        protected virtual bool CharBeforeNormalCompletion(char c)
        {
            return char.IsWhiteSpace(c) || c is ';' or '#' or '(' or ':' or '+' or '-' or '=' or '*' or '/' or '&' or ',';
        }

        protected virtual bool CharAtNormalCompletion(char c)
        {
            return char.IsLetterOrDigit(c) || c is '_';
        }

        public virtual OverloadProvider ConvertOverloadProvider(SignatureHelp signatureHelp)
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
                if (s.Documentation != null && s.Documentation.HasMarkupContent) m2 += s.Documentation.MarkupContent?.Value;
                if (s.Documentation != null && s.Documentation.HasString) m2 += s.Documentation.String;
                overloadOptions.Add((m1, m2.Length > 0 ? m2 : null));
            }

            return new OverloadProvider(overloadOptions);
        }

        public virtual IEnumerable<ICompletionData> ConvertCompletionData(CompletionList list)
        {
            //Parse completionitem
            foreach (var comp in list.Items) yield return ConvertCompletionItem(comp, _completionOffset);
        }

        protected virtual ICompletionData ConvertCompletionItem(CompletionItem comp, int offset)
        {
            var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
                ? instanceIcon
                : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

            void AfterComplete()
            {
                _ = ShowOverloadProviderAsync();
            }

            var description = comp.Documentation != null ? (comp.Documentation.MarkupContent != null ? comp.Documentation.MarkupContent.Value : comp.Documentation.String) : null;
            description = description?.Replace("\n", "\n\n");
            
            return new CompletionData(comp.InsertText ?? "", comp.Label, description, icon, 0,
                comp, offset, AfterComplete);
        }

        public ErrorListItem? GetErrorAtLocation(TextLocation location)
        {
            foreach (var error in ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFile))
                if (location.Line >= error.StartLine && location.Column >= error.StartColumn && (location.Line < error.EndLine || location.Line == error.EndLine && location.Column <= error.EndColumn))
                    return error;
            return null;
        }
        
        protected IEnumerable<TextDocumentContentChangeEvent> ConvertChanges(DocumentChangeEventArgs e)
        {
            var l = new List<TextDocumentContentChangeEvent>();
            var map = e.OffsetChangeMap;

            //Console.WriteLine("??" + map.Count + " " + e.Offset + " " + e.InsertedText);

            if (map.Count <= 1)
            {
                var m = e;
                var location = CodeBox.Document.GetLocation(m.Offset);
                //calculate newlines
                var newlines = e.RemovedText.Text.Count(x => x == '\n');
                var lastIndexNewLine = e.RemovedText.Text.LastIndexOf('\n');
                var lengthAfterLastNewLine = lastIndexNewLine >= 0
                    ? e.RemovedText.TextLength - lastIndexNewLine
                    : location.Column + e.RemovedText.TextLength;

                var endlocation = new TextLocation(location.Line + newlines, lengthAfterLastNewLine);

                var docChange = new TextDocumentContentChangeEvent
                {
                    Range = new Range
                    {
                        Start = new Position(location.Line - 1, location.Column - 1),
                        End = new Position(endlocation.Line - 1, endlocation.Column - 1)
                    },
                    Text = e.InsertedText.Text,
                    RangeLength = m.RemovalLength
                };

                l.Add(docChange);
                //Console.WriteLine("c Start: " + docChange.Range.Start.Line + " " + docChange.Range.Start.Character + " End: " + docChange.Range.End.Line + " " + docChange.Range.End.Character + " T: " + e.InsertedText.Text + " " + e.InsertedText.Text.Length);
            }
            else
            {
                throw new NotSupportedException("Multiple offsets???");
            }

            return l;
        }
    }
}