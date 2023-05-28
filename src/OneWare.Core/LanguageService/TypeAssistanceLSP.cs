using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OneWare.Core.EditorExtensions;
using OneWare.Core.ViewModels.DockViews;
using OneWare.ErrorList.Models;
using OneWare.ErrorList.ViewModels;
using OneWare.ProjectExplorer.Models;
using Prism.Ioc;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using CompletionList = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionList;
using IFile = OneWare.Shared.IFile;
using Location = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Core.LanguageService
{
    internal abstract class TypeAssistanceLsp : TypeAssistance, ITypeAssistance
    {
        private bool _completionBusy;
        private int _completionOffset;
        private DispatcherTimer? _dispatcherTimer;
        private TimeSpan _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;

        private TimeSpan _lastEditTime = DateTime.Now.TimeOfDay;
        private TimeSpan _lastRefreshTime = DateTime.Now.TimeOfDay;

        private ICompletionData? _lastSelectedCompletionItem;

        private readonly TimeSpan _timerTimeSpan = new(200);
        private static ISettingsService SettingsService => ContainerLocator.Container.Resolve<ISettingsService>();
        public IFile CurrentFile { get; }
        public string LineCommentSequence => "//";
        public LanguageServiceBase Service { get; }

        protected virtual TimeSpan RefreshTime => TimeSpan.FromMilliseconds(200);
        
        public TypeAssistanceLsp(IFile file, EditViewModel evm, LanguageServiceBase langService) : base(evm)
        {
            CurrentFile = file;
            Service = langService;
        }

        public virtual async Task<string?> GetHoverInfoAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady || !SettingsService.GetSettingValue<bool>("TypeAssistance_EnableHover")) return null;

            var location = CodeBox.Document.GetLocation(offset);

            var hover = await Service.RequestHoverAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (hover != null && !IsClosed)
            {
                if (hover.Contents.HasMarkedStrings)
                    return hover.Contents.MarkedStrings!.First().Value.Split('\n')[0]; //TODO what is this?
                if (hover.Contents.HasMarkupContent) return hover.Contents.MarkupContent?.Value;
            }

            return null;
        }

        public virtual async Task<List<MenuItemViewModel>?> GetQuickMenuAsync(int offset)
        {
            var menuItems = new List<MenuItemViewModel>();
            if (!Service.IsLanguageServiceReady || offset > CodeBox.Document.TextLength) return menuItems;
            var location = CodeBox.Document.GetLocation(offset);
            var pos = CodeBox.Document.GetPositionFromOffset(offset);

            //Quick Fixes
            var error = GetErrorAtLocation(location);
            if (error != null)
            {
                var codeactions = await Service.RequestCodeActionAsync(CurrentFile,
                    new Range
                    {
                        Start = new Position(error.StartLine - 1, error.StartColumn - 1 ?? 0),
                        End = new Position(error.EndLine - 1 ?? 0, error.EndColumn - 1 ?? 0)
                    });

                if (codeactions is not null && !IsClosed)
                {
                    var quickfixes = new List<MenuItemViewModel>();
                    foreach (var ca in codeactions)
                    {
                        if (ca.IsCodeAction && ca.CodeAction != null)
                        {
                            if(ca.CodeAction.Command != null) quickfixes.Add(new MenuItemViewModel
                            {
                                Header = ca.CodeAction.Title,
                                Command = new RelayCommand<Command>(ExecuteCommand),
                                CommandParameter = ca.CodeAction.Command
                            });
                            else if(ca.CodeAction.Edit != null) quickfixes.Add(new MenuItemViewModel
                            {
                                Header = ca.CodeAction.Title,
                                Command = new AsyncRelayCommand<WorkspaceEdit>(Service.ApplyWorkspaceEditAsync),
                                CommandParameter = ca.CodeAction.Edit
                            });
                        }
                    }

                    if (quickfixes.Count > 0)
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Quick fix...",
                            Items = quickfixes
                        });
                }
            }

            //Refactorings
            var prepareRefactor = await Service.PrepareRenameAsync(CurrentFile, pos);
            if (prepareRefactor != null)
                menuItems.Add(new MenuItemViewModel
                {
                    Header = "Rename...",
                    Command = new AsyncRelayCommand<RangeOrPlaceholderRange>(StartRenameSymbolAsync),
                    CommandParameter = prepareRefactor,
                    Icon = Application.Current?.FindResource("VsImageLib.Rename16X") as IImage
                });

            var definition = await Service.RequestDefinitionAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (definition != null && !IsClosed)
                foreach (var i in definition)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Definition",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Definition",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var declaration = await Service.RequestDeclarationAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (declaration != null && !IsClosed)
                foreach (var i in declaration)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Declaration",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Declaration",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var implementation = await Service.RequestImplementationAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (implementation != null && !IsClosed)
                foreach (var i in implementation)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Implementation",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Implentation",
                            Command = new RelayCommand<LocationLink>(GoToLocation),
                            CommandParameter = i.Location
                        });
            var typeDefinition = await Service.RequestImplementationAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (typeDefinition != null && !IsClosed)
                foreach (var i in typeDefinition)
                    if (i.IsLocation)
                        menuItems.Add(new MenuItemViewModel
                        {
                            Header = "Go to Type Definition",
                            Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                            CommandParameter = i.Location
                        });
                    else
                        menuItems.Add(new MenuItemViewModel
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
                var workspaceEdit = await Service.RequestRenameAsync(CurrentFile, range.Range.Start, newName);
                if (workspaceEdit != null && !IsClosed)
                    await Service.ApplyWorkspaceEditAsync(new ApplyWorkspaceEditParams
                        { Edit = workspaceEdit, Label = "Rename" });
            }
            else
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error("Placeholder Range renaming not supported yet!");
            }
        }
        
        public virtual async Task<Action?> GetActionOnControlWordAsync(int offset)
        {
            if (!Service.IsLanguageServiceReady || offset > CodeBox.Document.TextLength) return null;
            var location = CodeBox.Document.GetLocation(offset);

            var definition = await Service.RequestDefinitionAsync(CurrentFile,
                new Position(location.Line - 1, location.Column - 1));
            if (definition != null && !IsClosed)
                if (definition.FirstOrDefault() is { } loc)
                {
                    if (loc.IsLocation && loc.Location != null) return () => _ = GoToLocationAsync(loc.Location);
                    if (loc.IsLocationLink && loc.LocationLink != null) return () => GoToLocation(loc.LocationLink);
                }

            return null;
        }

        public virtual async Task GoToLocationAsync(Location? location)
        {
            if (location == null) return;
            
            var path = Path.GetFullPath(location.Uri.GetFileSystemPath());
            var entry = ContainerLocator.Container.Resolve<IProjectService>().Search(path) as IFile;
            entry ??= ContainerLocator.Container.Resolve<IProjectService>().GetTemporaryFile(path);
            
            if (entry is ProjectFile file)
            {
                var dockable = await ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(file);
                if (dockable is EditViewModel evm)
                {
                    var sOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.Start) - 1;
                    var eOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.End) - 1;
                    if (sOff >= 0 && eOff >= 0 && eOff > sOff) evm.Select(sOff, eOff - sOff);
                }
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
                    if (SettingsService.GetSettingValue<bool>("TypeAssistance_EnableAutoCompletion") && !_completionBusy)
                        await ShowOverloadProviderAsync();

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
                    CustomHighlightManager?.ClearHighlights();
                }
            }
            else
            {
                CustomHighlightManager?.ClearHighlights();
            }

            CodeBox.TextArea.TextView.Redraw();
        }

        public virtual async Task<List<(string, SymbolKind)>?> RetrieveSymbolsAsync()
        {
            var symbolInfo = await Service.RequestSymbolsAsync(CurrentFile);

            if (symbolInfo is null) return null;
            
            var highlights = new List<(string, SymbolKind)>();
            if (!IsClosed)
                foreach (var c in symbolInfo)
                    if (c.IsDocumentSymbol && c.DocumentSymbol != null)
                        //ContainerLocator.Container.Resolve<ILogger>()?.Log("DocumentSymbol not supported! TypeAssistanceLSP RetrieveSymbols()", ConsoleColor.Red);
                        highlights.Add((c.DocumentSymbol.Name, c.DocumentSymbol.Kind));
                    else if(c.IsDocumentSymbolInformation && c.SymbolInformation != null)
                        highlights.Add((c.SymbolInformation.Name, c.SymbolInformation.Kind));

            return highlights;
        }

        public virtual void SetCustomColor(SymbolKind kind, IEnumerable<(string, SymbolKind)> symbols)
        {
            var color = CodeBox.SyntaxHighlighting.GetNamedColor(kind.ToString());

            if (color == null) return;

            var highlights = symbols
                .Where(x => x.Item2 == kind)
                .Select(x => x.Item1)
                .ToArray();

            CustomHighlightManager?.SetHightlights(highlights, color);
        }

        public virtual async Task ShowOverloadProviderAsync()
        {
            var signatureHelp = await Service.RequestSignatureHelpAsync(CurrentFile,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1));
            if (signatureHelp != null && !IsClosed)
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

        public virtual async Task ShowCompletionAsync(string triggerChar, CompletionTriggerKind triggerKind)
        {
            var t = triggerChar.Length > 0 ? triggerChar[0] : ';';

            var completion = await Service.RequestCompletionAsync(CurrentFile,
                new Position(CodeBox.TextArea.Caret.Line - 1, CodeBox.TextArea.Caret.Column - 1), triggerChar,
                triggerKind);
            var custom = await GetCustomCompletionItemsAsync();

            if ((completion is not null || custom.Count > 0) && !IsClosed && Completion != null)
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

        public virtual List<MenuItemViewModel>? GetTypeAssistanceQuickOptions()
        {
            return new List<MenuItemViewModel>
            {
                new()
                {
                    Header = "Restart Language Server",
                    Command = new RelayCommand(() => _ = Service.RestartAsync()),
                    Icon = Application.Current?.FindResource("VsImageLib.RefreshGrey16X") as IImage
                }
            };
        }

        public virtual void ExecuteCommand(Command? cmd)
        {
            if(cmd == null) return;
            if (Service.IsLanguageServiceReady && !IsClosed) _ = Service.ExecuteCommandAsync(cmd);
        }

        public virtual async Task ResolveCompletionAsync()
        {
            //Resolve selected item
            if (Service.IsLanguageServiceReady && Completion != null && Completion.IsOpen &&
                _lastSelectedCompletionItem is CompletionData completionLsp && completionLsp.CompletionItemLsp != null)
            {
                var resolvedCi = await Service.ResolveCompletionItemAsync(completionLsp.CompletionItemLsp);
                if (resolvedCi != null && !IsClosed && Completion.IsOpen)
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

        public bool CharBeforeNormalCompletion(char c)
        {
            return char.IsWhiteSpace(c) || c is ';' or '#' or '(' or ':' or '+' or '-' or '=' or '*' or '/' or '&' or ',';
        }

        public bool CharAtNormalCompletion(char c)
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

        public virtual ICompletionData ConvertCompletionItem(CompletionItem comp, int offset)
        {
            var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
                ? instanceIcon
                : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

            Action afterComplete = () => { _ = ShowOverloadProviderAsync(); };
            
            var descr = comp.Documentation != null ? (comp.Documentation.MarkupContent != null ? comp.Documentation.MarkupContent.Value : comp.Documentation.String) : null;
            
            if (comp.InsertTextFormat == InsertTextFormat.PlainText)
                return new CompletionData(comp.InsertText ?? "", comp.Label ?? "", descr, icon,
                    0, comp, offset, afterComplete);
            return new CompletionData(comp.InsertText ?? "", comp.Label ?? "", descr, icon, 0,
                comp, offset, afterComplete);
        }

        public ErrorListItemModel? GetErrorAtLocation(TextLocation location)
        {
            foreach (var error in ContainerLocator.Container.Resolve<ErrorListViewModel>().GetErrorsForFile(CurrentFile))
                if (location.Line >= error.StartLine && location.Column >= error.StartColumn && (location.Line < error.EndLine || location.Line == error.EndLine && location.Column <= error.EndColumn))
                    return error;
            return null;
        }

        #region Initialisation

        public override void Initialize(CompletionWindow completion)
        {
            base.Initialize(completion);
            
            completion.CompletionList.SelectionChanged += (o, i) =>
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

            if (CodeBox.SyntaxHighlighting != null)
                CustomHighlightManager = new CustomHighlightManager(CodeBox.SyntaxHighlighting);

            Service.LanguageServiceActivated += Server_Activated;
            Service.LanguageServiceDeactivated += Server_Deactivated;

            if (Service.IsLanguageServiceReady) OnServerActivated();

            CodeBox.Document.Changed -= DocumentChanged;
            CodeBox.Document.Changed += DocumentChanged;
            ViewModel.FileSaved -= FileSaved;
            ViewModel.FileSaved += FileSaved;

            _dispatcherTimer.Start();
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
                Service.RefreshTextDocument(CurrentFile, CodeBox.Text);
            }
            _ = UpdateSymbolsAsync();
        }

        private void Server_Activated(object? sender, EventArgs e)
        {
            OnServerActivated();
        }

        private void Server_Deactivated(object? sender, EventArgs e)
        {
            OnServerDeactivated();
        }

        protected override void OnServerActivated()
        {
            if (IsClosed) return;

            base.OnServerActivated();
            Service.DidOpenTextDocument(CurrentFile, ViewModel.CurrentDocument.Text);

            _ = UpdateSymbolsAsync();
        }

        protected override void OnServerDeactivated()
        {
            if (IsClosed) return;
            base.OnServerDeactivated();
        }

        protected virtual void DocumentChanged(object? sender, DocumentChangeEventArgs e)
        {
            if (IsClosed || !Service.IsLanguageServiceReady) return;

            if (Service.Client?.ServerSettings.Capabilities.TextDocumentSync?.Kind is TextDocumentSyncKind
                    .Incremental or TextDocumentSyncKind.None)
            {
                var c = ConvertChanges(e);
                var changes = new Container<TextDocumentContentChangeEvent>(c);
                Service.RefreshTextDocument(CurrentFile, changes);
            }

            _lastEditTime = DateTime.Now.TimeOfDay;
        }

        private void FileSaved(object? sender, EventArgs e)
        {
            if (Service.IsLanguageServiceReady) Service.DidSaveTextDocument(CurrentFile, ViewModel.CurrentDocument.Text);
        }

        public override void Close()
        {
            CodeBox.Document.Changed -= DocumentChanged;
            ViewModel.FileSaved -= FileSaved;
            Service.LanguageServiceActivated -= Server_Activated;
            if (_dispatcherTimer != null)
            {
                _dispatcherTimer.Tick -= Timer_Tick;
                _dispatcherTimer.Stop();
            }

            base.Close();
            if (Service.IsLanguageServiceReady) Service.DidCloseTextDocument(CurrentFile);
        }

        #endregion
    }
}