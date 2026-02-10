using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using CompletionList = OmniSharp.Extensions.LanguageServer.Protocol.Models.CompletionList;
using InlayHint = OneWare.Essentials.EditorExtensions.InlayHint;
using Location = OmniSharp.Extensions.LanguageServer.Protocol.Models.Location;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace OneWare.Essentials.LanguageService;

/// <summary>
///     Type Assistance that uses ILanguageService to perform assistance
/// </summary>
public abstract class TypeAssistanceLanguageService : TypeAssistanceBase
{
    private readonly IBrush _highlightBackground = SolidColorBrush.Parse("#3300c8ff");
    private TimeSpan _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;

    private readonly TimeSpan _timerTimeSpan = TimeSpan.FromMilliseconds(100);

    private bool _completionBusy;

    private CompositeDisposable _completionDisposable = new();
    private DispatcherTimer? _dispatcherTimer;
    private TimeSpan _lastCaretChangedRefreshTime = DateTime.Now.TimeOfDay;
    private TimeSpan _lastCaretChangeTime = DateTime.Now.TimeOfDay;
    private TimeSpan _lastCompletionItemResolveTime = DateTime.Now.TimeOfDay;
    private TimeSpan _lastDocumentChangedRefreshTime = DateTime.Now.TimeOfDay;

    private TimeSpan _lastEditTime = DateTime.Now.TimeOfDay;

    protected TypeAssistanceLanguageService(IEditor evm, ILanguageService langService) : base(evm)
    {
        Service = langService;
    }

    private static ISettingsService SettingsService => ContainerLocator.Container.Resolve<ISettingsService>();
    protected virtual TimeSpan DocumentChangedRefreshTime => TimeSpan.FromMilliseconds(500);
    protected virtual TimeSpan CaretChangedRefreshTime => TimeSpan.FromMilliseconds(100);

    protected ILanguageService Service { get; }

    public override async Task<string?> GetHoverInfoAsync(int offset)
    {
        if (!Service.IsLanguageServiceReady) return null;

        var pos = CodeBox.Document.GetLocation(offset);

        var error = ContainerLocator.Container.Resolve<IErrorService>().GetErrorsForFile(CurrentFilePath)
            .OrderBy(x => x.Type)
            .FirstOrDefault(error => pos.Line >= error.StartLine
                                     && pos.Line <= error.EndLine
                                     && pos.Column >= error.StartColumn
                                     && pos.Column <= error.EndColumn);
        var parts = new List<string>();
        if (error != null) parts.Add(error.Description);

        var hover = await Service.RequestHoverAsync(CurrentFilePath,
            new Position(pos.Line - 1, pos.Column - 1));
        if (hover != null)
        {
            var hoverText = BuildHoverText(hover);
            if (!string.IsNullOrWhiteSpace(hoverText)) parts.Add(hoverText);
        }

        var info = string.Join("\n\n", parts);
        return string.IsNullOrWhiteSpace(info) ? null : info;
    }

    private static string? BuildHoverText(Hover hover)
    {
        if (hover.Contents.HasMarkupContent)
            return hover.Contents.MarkupContent?.Value;

        if (hover.Contents is { HasMarkedStrings: true, MarkedStrings: not null })
        {
            var segments = new List<string>();
            foreach (var marked in hover.Contents.MarkedStrings)
            {
                if (!string.IsNullOrWhiteSpace(marked.Language))
                    segments.Add($"```{marked.Language}\n{marked.Value}\n```");
                else
                    segments.Add(marked.Value);
            }

            return segments.Count > 0 ? string.Join("\n\n", segments) : null;
        }

        return null;
    }

    public override async Task<List<MenuItemModel>?> GetQuickMenuAsync(int offset)
    {
        var menuItems = new List<MenuItemModel>();
        if (!Service.IsLanguageServiceReady || offset > CodeBox.Document.TextLength) return menuItems;
        var location = CodeBox.Document.GetLocation(offset);
        var pos = CodeBox.Document.GetPositionFromOffset(offset);

        //Quick Fixes
        var error = GetErrorAtLocation(location);
        if (error != null && error.Diagnostic != null)
        {
            var codeactions = await Service.RequestCodeActionAsync(CurrentFilePath,
                new Range
                {
                    Start = new Position(Math.Max(error.StartLine - 1, 0), Math.Max(error.StartColumn - 1 ?? 0, 0)),
                    End = new Position(Math.Max(error.EndLine - 1 ?? 0, 0), Math.Max(error.EndColumn - 1 ?? 0, 0))
                }, error.Diagnostic);

            if (codeactions is not null && IsOpen)
            {
                var quickfixes = new ObservableCollection<MenuItemModel>();
                foreach (var ca in codeactions)
                    if (ca.IsCodeAction && ca.CodeAction != null)
                    {
                        if (ca.CodeAction.Command != null)
                            quickfixes.Add(new MenuItemModel(ca.CodeAction.Title)
                            {
                                Header = ca.CodeAction.Title,
                                Command = new RelayCommand<Command>(ExecuteCommand),
                                CommandParameter = ca.CodeAction.Command
                            });
                        else if (ca.CodeAction.Edit != null)
                            quickfixes.Add(new MenuItemModel(ca.CodeAction.Title)
                            {
                                Header = ca.CodeAction.Title,
                                Command = new AsyncRelayCommand<WorkspaceEdit>(Service.ApplyWorkspaceEditAsync),
                                CommandParameter = ca.CodeAction.Edit
                            });
                    }

                if (quickfixes.Count > 0)
                    menuItems.Add(new MenuItemModel("Quick fix")
                    {
                        Header = "Quick fix...",
                        Items = quickfixes
                    });
            }
        }

        //Refactorings
        var prepareRefactor = await Service.PrepareRenameAsync(CurrentFilePath, pos);
        if (prepareRefactor != null)
            menuItems.Add(new MenuItemModel("Rename")
            {
                Header = "Rename...",
                Command = new AsyncRelayCommand<RangeOrPlaceholderRange>(StartRenameSymbolAsync),
                CommandParameter = prepareRefactor,
                Icon = new IconModel("VsImageLib.Rename16X")
            });

        var definition = await Service.RequestDefinitionAsync(CurrentFilePath,
            new Position(location.Line - 1, location.Column - 1));
        var definitionMenuItem = CreateLocationMenuItem("GoToDefinition", "Go to Definition", definition);
        if (definitionMenuItem != null && IsOpen) menuItems.Add(definitionMenuItem);

        var declaration = await Service.RequestDeclarationAsync(CurrentFilePath,
            new Position(location.Line - 1, location.Column - 1));
        var declarationMenuItem = CreateLocationMenuItem("GoToDeclaration", "Go to Declaration", declaration);
        if (declarationMenuItem != null && IsOpen) menuItems.Add(declarationMenuItem);

        var implementation = await Service.RequestImplementationAsync(CurrentFilePath,
            new Position(location.Line - 1, location.Column - 1));
        var implementationMenuItem = CreateLocationMenuItem("GoToImplementation", "Go to Implementation", implementation);
        if (implementationMenuItem != null && IsOpen) menuItems.Add(implementationMenuItem);

        var typeDefinition = await Service.RequestTypeDefinitionAsync(CurrentFilePath,
            new Position(location.Line - 1, location.Column - 1));
        var typeDefinitionMenuItem =
            CreateLocationMenuItem("GoToTypeDefinition", "Go to Type Definition", typeDefinition);
        if (typeDefinitionMenuItem != null && IsOpen) menuItems.Add(typeDefinitionMenuItem);
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
            var workspaceEdit = await Service.RequestRenameAsync(CurrentFilePath, range.Range.Start, newName);
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

        var definition = await Service.RequestDefinitionAsync(CurrentFilePath,
            new Position(location.Line - 1, location.Column - 1));
        if (definition != null && IsOpen)
        {
            var normalized = NormalizeLocations(definition);
            if (normalized.Count > 0)
                return () => _ = GoToLocationAsync(normalized[0]);
        }

        return null;
    }

    protected virtual async Task GoToLocationAsync(Location? location)
    {
        if (location == null) return;

        var path = Path.GetFullPath(location.Uri.GetFileSystemPath());
        var dockable = await ContainerLocator.Container.Resolve<IMainDockService>()
            .OpenFileAsync(path);
        if (dockable is IEditor evm)
        {
            var sOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.Start) - 1;
            var eOff = evm.CurrentDocument.GetOffsetFromPosition(location.Range.End) - 1;
            if (sOff >= 0 && eOff >= 0 && eOff > sOff) evm.Select(sOff, eOff - sOff);
        }
    }

    public virtual void GoToLocation(LocationLink? location)
    {
        if (location == null) return;

        var targetUri = location.TargetUri;
        var targetRange = location.TargetSelectionRange ?? location.TargetRange;
        if (targetUri == null || targetRange == null) return;

        _ = GoToLocationAsync(new Location
        {
            Uri = targetUri,
            Range = targetRange
        });
    }

    private MenuItemModel? CreateLocationMenuItem(string id, string header,
        IEnumerable<LocationOrLocationLink>? locations)
    {
        if (locations == null) return null;

        var normalized = NormalizeLocations(locations);
        if (normalized.Count == 0) return null;

        if (normalized.Count == 1)
            return new MenuItemModel(id)
            {
                Header = header,
                Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                CommandParameter = normalized[0]
            };

        var items = new ObservableCollection<MenuItemModel>();
        var index = 1;
        foreach (var location in normalized)
        {
            items.Add(new MenuItemModel(id + "Item")
            {
                Header = FormatLocationHeader(location, index++),
                Command = new AsyncRelayCommand<Location>(GoToLocationAsync),
                CommandParameter = location
            });
        }

        return new MenuItemModel(id)
        {
            Header = header,
            Items = items
        };
    }

    private List<Location> NormalizeLocations(IEnumerable<LocationOrLocationLink> locations)
    {
        var result = new List<Location>();
        var seen = new HashSet<LocationKey>();

        foreach (var entry in locations)
        {
            if (!TryGetLocation(entry, out var location, out var key)) continue;
            if (seen.Add(key)) result.Add(location);
        }

        return result;
    }

    private static bool TryGetLocation(LocationOrLocationLink entry, out Location location, out LocationKey key)
    {
        location = null!;
        key = default;

        if (entry.IsLocation && entry.Location != null)
        {
            location = entry.Location;
        }
        else if (entry.IsLocationLink && entry.LocationLink != null)
        {
            var targetRange = entry.LocationLink.TargetSelectionRange ?? entry.LocationLink.TargetRange;
            if (entry.LocationLink.TargetUri == null || targetRange == null) return false;

            location = new Location
            {
                Uri = entry.LocationLink.TargetUri,
                Range = targetRange
            };
        }
        else
        {
            return false;
        }

        var path = location.Uri.GetFileSystemPath();
        var keyPath = string.IsNullOrWhiteSpace(path) ? location.Uri.ToString() : path.ToPathKey();
        var range = location.Range;

        key = new LocationKey(keyPath, range.Start.Line, range.Start.Character, range.End.Line, range.End.Character);
        return true;
    }

    private static string FormatLocationHeader(Location location, int index)
    {
        var path = location.Uri?.GetFileSystemPath();
        var fileName = string.IsNullOrWhiteSpace(path) ? location.Uri?.ToString() ?? "Unknown" : Path.GetFileName(path);
        var line = location.Range.Start.Line + 1;
        var column = location.Range.Start.Character + 1;
        return $"{index}. {fileName}:{line}:{column}";
    }

    private readonly record struct LocationKey(string PathKey, int StartLine, int StartCharacter, int EndLine,
        int EndCharacter);

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

            if ((CodeBox.Text[startIndex] == '(' && CodeBox.Text[endIndex] == ')') ||
                (CodeBox.Text[startIndex] == '{' && CodeBox.Text[endIndex] == '}'))
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
        var result = await Service.RequestDocumentHighlightAsync(CurrentFilePath,
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
        var tokens = await Service.RequestSemanticTokensFullAsync(CurrentFilePath);

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
        if (CodeBox.Document.LineCount == 0) return;

        var inlayHintContainer =
            await Service.RequestInlayHintsAsync(CurrentFilePath,
                new Range(0, 0, CodeBox.Document.LineCount,
                    CodeBox.Document.GetLineByNumber(CodeBox.Document.LineCount).Length));

        if (inlayHintContainer is not null)
            Editor.Editor.InlayHintGenerator.SetInlineHints(inlayHintContainer.Select(x => new InlayHint
            {
                Offset = Editor.CurrentDocument.GetOffset(x.Position.Line + 1, x.Position.Character + 1),
                Text = x.Label.HasInlayHintLabelParts
                    ? x.Label.InlayHintLabelParts?.FirstOrDefault()?.Value ?? ""
                    : x.Label.String ?? ""
            }));
        else
            Editor.Editor.InlayHintGenerator.ClearInlineHints();
    }

    protected virtual async Task ShowSignatureHelpAsync(SignatureHelpTriggerKind triggerKind, string? triggerChar,
        bool retrigger, SignatureHelp? activeSignatureHelp)
    {
        var signatureHelp = await Service.RequestSignatureHelpAsync(CurrentFilePath,
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

    protected virtual async Task ShowCompletionAsync(CompletionTriggerKind triggerKind, string? triggerChar)
    {
        //Console.WriteLine($"Completion request kind: {triggerKind} char: {triggerChar}");

        var completionOffset = CodeBox.CaretOffset;
        if (triggerKind is CompletionTriggerKind.Invoked)
            completionOffset = Math.Max(0, completionOffset - 1);

        var lspCompletionItems = await Service.RequestCompletionAsync(CurrentFilePath,
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
                EndOffset = CodeBox.CaretOffset
            };

            Observable.FromEventPattern(Completion, nameof(Completion.Closed)).Take(1).Subscribe(x =>
            {
                _completionDisposable.Dispose();
            }).DisposeWith(_completionDisposable);

            Completion.CompletionList.ListBox.WhenValueChanged(x => x.ItemsSource).Subscribe(x =>
            {
                if (Completion.CompletionList.ListBox.Items.Count == 0) Completion.Close();
            }).DisposeWith(_completionDisposable);
            Completion.CompletionList.ListBox.WhenValueChanged(x => x.SelectedItem).Subscribe(_ =>
            {
                _lastCompletionItemChangedTime = DateTime.Now.TimeOfDay;
            }).DisposeWith(_completionDisposable);

            if (lspCompletionItems is not null)
            {
                if (triggerKind is CompletionTriggerKind.TriggerCharacter && triggerChar != null) completionOffset++;

                Completion.CompletionList.CompletionData.AddRange(ConvertCompletionData(lspCompletionItems,
                    completionOffset));
            }

            foreach (var customItem in customCompletionItems)
            {
                var insert = false;
                for (var c = 0; c < Completion.CompletionList.CompletionData.Count; c++)
                {
                    var compare = string.Compare(Completion.CompletionList.CompletionData[c].Label, customItem.Label,
                        StringComparison.Ordinal);

                    if (compare > 0)
                    {
                        Completion.CompletionList.CompletionData.Insert(c, customItem);
                        insert = true;
                        break;
                    }

                    if (compare == 0)
                    {
                        //Do not insert duplicates
                        insert = true;
                        break;
                    }
                }

                if (!insert) Completion.CompletionList.CompletionData.Add(customItem);
            }

            //Calculate CompletionWindow width
            var length = 0;
            foreach (var data in Completion.CompletionList.CompletionData)
            {
                var contentLength = data.Label.Length;
                var detailLength = (data as CompletionData)?.Detail?.Length ?? 0;

                var visibleChars = contentLength + detailLength + 5;

                if (visibleChars > length) length = visibleChars;
            }

            var calculatedWith = length * SettingsService.GetSettingValue<int>("Editor_FontSize") + 50;

            Completion.Width = calculatedWith > 400 ? 500 : calculatedWith;

            if (Completion.CompletionList.CompletionData.Count > 0)
            {
                Completion.Show();
                if (triggerKind is not CompletionTriggerKind.TriggerCharacter)
                    Completion.CompletionList.SelectItem(triggerChar);
            }
        }
    }

    protected virtual Task<List<CompletionData>> GetCustomCompletionItemsAsync()
    {
        return Task.FromResult(new List<CompletionData>());
    }

    public override IEnumerable<MenuItemModel> GetTypeAssistanceQuickOptions()
    {
        return new List<MenuItemModel>
        {
            new("RestartLs")
            {
                Header = "Restart Language Server",
                Command = new RelayCommand(() => _ = Service.RestartAsync()),
                Icon = new IconModel("VsImageLib.RefreshGrey16X")
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
            var signature = FormatSignatureLabel(s);
            var docs = ExtractDocumentation(s.Documentation);

            if (s.Parameters is not null && s.ActiveParameter.HasValue)
            {
                var index = s.ActiveParameter.Value;
                if (index >= 0 && index < s.Parameters.Count())
                {
                    var paramDoc = ExtractDocumentation(s.Parameters.ElementAt(index).Documentation);
                    if (!string.IsNullOrWhiteSpace(paramDoc))
                        docs = string.IsNullOrWhiteSpace(docs) ? paramDoc : docs + "\n\n" + paramDoc;
                }
            }

            overloadOptions.Add((signature, string.IsNullOrWhiteSpace(docs) ? null : docs));
        }

        return new OverloadProvider(overloadOptions)
        {
            SignatureHelp = signatureHelp
        };
    }

    protected virtual IEnumerable<ICompletionData> ConvertCompletionData(CompletionList list, int offset)
    {
        var items = list.Items.ToList();
        if (items.Any(x => !string.IsNullOrWhiteSpace(x.SortText)))
            items = items.OrderBy(x => string.IsNullOrWhiteSpace(x.SortText) ? x.Label : x.SortText,
                StringComparer.Ordinal).ToList();

        var priority = items.Count;
        foreach (var comp in items)
            yield return ConvertCompletionItem(comp, offset, priority--);
    }

    protected virtual ICompletionData ConvertCompletionItem(CompletionItem comp, int offset, double priority = 0)
    {
        var icon = TypeAssistanceIconStore.Instance.Icons.TryGetValue(comp.Kind, out var instanceIcon)
            ? instanceIcon
            : TypeAssistanceIconStore.Instance.CustomIcons["Default"];

        void AfterComplete()
        {
            if (comp.AdditionalTextEdits is not null && comp.AdditionalTextEdits.Any())
                Service.ApplyContainer(CurrentFilePath, comp.AdditionalTextEdits);
            if (comp.Command != null)
                _ = Service.ExecuteCommandAsync(comp.Command);
            _ = ShowSignatureHelpAsync(SignatureHelpTriggerKind.Invoked, null, false, null);
        }

        var description = ExtractDocumentation(comp.Documentation);
        var insertText = comp.InsertText ?? comp.Label;
        var isSnippet = comp.InsertTextFormat == InsertTextFormat.Snippet;
        int? replaceStart = null;
        int? replaceEnd = null;

        if (comp.TextEdit != null)
        {
            if (comp.TextEdit.IsTextEdit && comp.TextEdit.TextEdit != null)
            {
                insertText = comp.TextEdit.TextEdit.NewText;
                replaceStart = CodeBox.Document.GetOffsetFromPosition(comp.TextEdit.TextEdit.Range.Start) - 1;
                replaceEnd = CodeBox.Document.GetOffsetFromPosition(comp.TextEdit.TextEdit.Range.End) - 1;
            }
            else if (comp.TextEdit.IsInsertReplaceEdit && comp.TextEdit.InsertReplaceEdit != null)
            {
                insertText = comp.TextEdit.InsertReplaceEdit.NewText;
                replaceStart = CodeBox.Document.GetOffsetFromPosition(comp.TextEdit.InsertReplaceEdit.Replace.Start) - 1;
                replaceEnd = CodeBox.Document.GetOffsetFromPosition(comp.TextEdit.InsertReplaceEdit.Replace.End) - 1;
            }
        }

        return new CompletionData(insertText, comp.Label, comp.Detail, description, icon,
            priority,
            comp, offset, CurrentFilePath, AfterComplete, isSnippet)
        {
            FilterText = comp.FilterText,
            SortText = comp.SortText,
            ReplaceStartOffset = replaceStart,
            ReplaceEndOffset = replaceEnd
        };
    }

    private static string? ExtractDocumentation(StringOrMarkupContent? documentation)
    {
        if (documentation == null) return null;
        if (documentation.HasMarkupContent) return documentation.MarkupContent?.Value;
        if (documentation.HasString) return documentation.String;
        return null;
    }

    private static string FormatSignatureLabel(SignatureInformation signature)
    {
        var label = signature.Label ?? string.Empty;
        if (signature.Parameters is null || !signature.ActiveParameter.HasValue) return label;

        var index = signature.ActiveParameter.Value;
        if (index < 0 || index >= signature.Parameters.Count()) return label;

        var paramLabel = GetParameterLabelText(signature.Parameters.ElementAt(index).Label);
        if (string.IsNullOrWhiteSpace(paramLabel)) return label;

        var paramIndex = label.IndexOf(paramLabel, StringComparison.Ordinal);
        if (paramIndex < 0) return label;

        return label.Remove(paramIndex, paramLabel.Length).Insert(paramIndex, $"**{paramLabel}**");
    }

    private static string? GetParameterLabelText(object? label)
    {
        if (label == null) return null;
        if (label is string text) return text;
        var asString = label.ToString();
        return string.IsNullOrWhiteSpace(asString) ? null : asString;
    }

    public ErrorListItem? GetErrorAtLocation(TextLocation location)
    {
        foreach (var error in ContainerLocator.Container.Resolve<IErrorService>()
                     .GetErrorsForFile(CurrentFilePath))
            if (location.Line >= error.StartLine && location.Column >= error.StartColumn &&
                (location.Line < error.EndLine ||
                 (location.Line == error.EndLine && location.Column <= error.EndColumn)))
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
        if (_lastEditTime > _lastDocumentChangedRefreshTime &&
            _lastEditTime <= DateTime.Now.TimeOfDay - DocumentChangedRefreshTime)
        {
            _lastDocumentChangedRefreshTime = DateTime.Now.TimeOfDay;

            //Execute slower actions after no edit was done for 500ms
            CodeUpdated();
        }

        if (_lastCaretChangeTime > _lastCaretChangedRefreshTime &&
            _lastCaretChangeTime <= DateTime.Now.TimeOfDay - CaretChangedRefreshTime)
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
        Service.RefreshTextDocument(CurrentFilePath, CodeBox.Text);
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
        Service.DidOpenTextDocument(CurrentFilePath, Editor.CurrentDocument.Text);

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
        Service.RefreshTextDocument(CurrentFilePath, changes);

        _lastEditTime = DateTime.Now.TimeOfDay;
    }

    private void FileSaved(object? sender, EventArgs e)
    {
        if (Service.IsLanguageServiceReady)
            Service.DidSaveTextDocument(CurrentFilePath, Editor.CurrentDocument.Text);
    }

    public override void Close()
    {
        CodeBox.Document.Changed -= DocumentChanged;
        Editor.FileSaved -= FileSaved;
        Service.LanguageServiceActivated -= Server_Activated;
        Service.LanguageServiceDeactivated -= Server_Deactivated;
        if (_dispatcherTimer != null)
        {
            _dispatcherTimer.Tick -= Timer_Tick;
            _dispatcherTimer.Stop();
        }

        base.Close();
        if (Service.IsLanguageServiceReady) Service.DidCloseTextDocument(CurrentFilePath);
    }

    #endregion
}
