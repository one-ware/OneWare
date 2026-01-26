using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Markdown.Avalonia;
using OneWare.Core.Extensions;
using OneWare.Core.Models;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Core.Views.Controls;
using OneWare.ErrorList.ViewModels;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using Range = System.Range;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;

namespace OneWare.Core.Views.DockViews;

public partial class EditView : UserControl
{
    private readonly IErrorService _errorService;
    private readonly ISettingsService _settingsService;

    private CompositeDisposable _compositeDisposable = new();

    private IEnumerable<int> _lastSearchResultLines = new List<int>();

    private ITypeAssistance? _typeAssistance;

    public EditView()
    {
        _settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
        _errorService = ContainerLocator.Container.Resolve<IErrorService>();

        InitializeComponent();

        var localComposite = new CompositeDisposable();

        DataContextChanged += (_, _) =>
        {
            localComposite.Dispose();
            localComposite = new CompositeDisposable();

            if (DataContext is not EditViewModel evm) return;
            ViewModel = evm;

            Observable.FromEventPattern(h => CodeBox.LayoutUpdated += h, h => CodeBox.LayoutUpdated -= h)
                .Take(1)
                .Subscribe(_ =>
                {
                    //EditView is ready at this point
                    ViewModel.WhenValueChanged(e => e.TypeAssistance)
                        .Subscribe(t =>
                        {
                            _typeAssistance?.Detach();
                            _typeAssistance = t;
                            Setup();
                        }).DisposeWith(localComposite);
                }).DisposeWith(localComposite);
        };
    }

    private ExtendedTextEditor CodeBox =>
        ViewModel?.Editor ?? throw new NullReferenceException(nameof(CodeBox));

    public EditViewModel? ViewModel { get; set; }
    public ObjectValueModel? VaribleViewDataConext { get; }

    private void Reset()
    {
        _compositeDisposable.Dispose();
        _compositeDisposable = new CompositeDisposable();
        _typeAssistance?.Detach();
        CodeBox.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
        CodeBox.ModificationService.ClearModification("Control_Underline");
        DetachEvents();
    }

    private void Setup()
    {
        Reset();

        //Attach Events
        AttachEvents();

        try
        {
            _typeAssistance?.Attach();
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }

        TopLevel.GetTopLevel(this)?.AddDisposableHandler(KeyDownEvent, (o, e) =>
        {
            if (PlatformHelper.IsControl(e))
            {
                if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
                _ = GetControlHoverActionAsync();
            }
        }, RoutingStrategies.Tunnel, true).DisposeWith(_compositeDisposable);

        TopLevel.GetTopLevel(this)?.AddDisposableHandler(KeyUpEvent, (o, e) =>
        {
            if (PlatformHelper.IsControl(e))
            {
                _controlAction = null;
                CodeBox.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
                CodeBox.ModificationService.ClearModification("Control_Underline");
            }
        }, RoutingStrategies.Tunnel, true).DisposeWith(_compositeDisposable);

        //Zoom in ctrl + wheel
        CodeBox.AddDisposableHandler(PointerWheelChangedEvent, (o, i) =>
        {
            if (i.KeyModifiers != PlatformHelper.ControlKey) return;
            var fontSize = _settingsService.GetSettingValue<int>("Editor_FontSize");
            var newFontSize = i.Delta.Y > 0 ? fontSize + 1 : fontSize - 1;
            if (newFontSize < 5) newFontSize = 5;
            if (newFontSize > 50) newFontSize = 50;
            _settingsService.SetSettingValue("Editor_FontSize", newFontSize);

            i.Handled = true;
        }, RoutingStrategies.Tunnel, true).DisposeWith(_compositeDisposable);

        (TopLevel.GetTopLevel(this) as Window)?.WhenValueChanged(x => x.IsKeyboardFocusWithin).Subscribe(
                x =>
                {
                    if (!x) HoverBox.Close();
                })
            .DisposeWith(_compositeDisposable);
    }

    //-------------------------Highlighting----------------------------------------------//

    #region Highlighting

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CodeBox.TextArea.ContextMenu?.Close();
        HoverBox.Close();

        if (ViewModel?.DisableEditViewEvents ?? true) return;

        _typeAssistance?.CaretPositionChanged(CodeBox.CaretOffset);

        var searcher = new CBracketSearcher();
        CodeBox.BracketRenderer.SetHighlight(searcher.SearchBracket(CodeBox.Document, CodeBox.CaretOffset));
    }

    #endregion

    //-------------------------General + Events------------------------------------------//

    #region General

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        CodeBox.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
        CodeBox.ModificationService.ClearModification("Control_Underline");
        base.OnLostFocus(e);
    }

    private void AttachEvents()
    {
        CodeBox.TextArea.TextView.ScrollOffsetChanged += ScrollOffsetChanged;
        CodeBox.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        CodeBox.TextArea.TextEntering += TextEditor_TextArea_TextEntering;
        CodeBox.TextArea.TextEntered += TextEditor_TextArea_TextEntered;
        CodeBox.Document.TextChanged += Text_Changed;
        CodeBox.PointerHover += Pointer_Hover;
        CodeBox.PointerMoved += Pointer_Moved;
        CodeBox.PointerExited += Pointer_Exited;
        CodeBox.AddHandler(KeyDownEvent, TextBox_KeyDown, RoutingStrategies.Bubble, true);

        CodeBox.AddHandler(PointerPressedEvent, PointerPressedBeforeCaretUpdate, RoutingStrategies.Tunnel);
        CodeBox.AddHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate, RoutingStrategies.Bubble, true);

        CodeBox.SearchPanel.OnSearch += Search_Updated;
    }

    private void DetachEvents()
    {
        CodeBox.TextArea.TextView.ScrollOffsetChanged -= ScrollOffsetChanged;
        CodeBox.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
        CodeBox.TextArea.TextEntering -= TextEditor_TextArea_TextEntering;
        CodeBox.TextArea.TextEntered -= TextEditor_TextArea_TextEntered;
        CodeBox.Document.TextChanged -= Text_Changed;
        CodeBox.PointerHover -= Pointer_Hover;
        CodeBox.PointerMoved -= Pointer_Moved;
        CodeBox.PointerExited -= Pointer_Exited;
        CodeBox.RemoveHandler(KeyDownEvent, TextBox_KeyDown);

        CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedBeforeCaretUpdate);
        CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate);

        if (CodeBox.SearchPanel != null)
            CodeBox.SearchPanel.OnSearch -= Search_Updated;
    }

    private void Search_Updated(object? sender, IEnumerable<SearchResult> results)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        try
        {
            if (ViewModel == null) return;

            _lastSearchResultLines =
                results.Select(x => CodeBox.Document.GetLineByOffset(x.StartOffset).LineNumber);

            ViewModel.ScrollInfo.Refresh("searchResult", _lastSearchResultLines
                .Distinct()
                .Select(x => new ScrollInfoLine(x, _searchResultScrollBrush))
                .ToArray());

            CodeBox.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
        }
    }

    // public void UpdateDebuggerLine(BreakPoint active)
    // {
    //     CodeBox.TextArea.TextView.LineTransformers.RemoveMany(CodeBox.TextArea.TextView.LineTransformers
    //         .Where(b => b is LineColorizer { Id: "DebuggerLine" }));
    //     if (active != null && active.File.EqualPaths(CurrentFile.FullPath))
    //         CodeBox.TextArea.TextView.LineTransformers.Add(
    //             new LineColorizer(active.Line, null,
    //                 Application.Current.FindResource("DebuggerBreakLine") as IBrush, "DebuggerLine"));
    // }

    public void PointerPressedAfterCaretUpdate(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        _ = PointerPressedAfterCaretUpdateAsync(sender, e);
    }

    private void Text_Changed(object? sender, EventArgs e)
    {
        CodeBox.WordRenderer.SetHighlight(null); //Reset wordhighlight
        ViewModel?.ScrollInfo.Refresh("wordRenderer");
        CodeBox.BracketRenderer.SetHighlight(null);
    }

    private void ScrollOffsetChanged(object? sender, EventArgs e)
    {
        HoverBox.Close();
        CodeBox?.TextArea.ContextMenu?.Close();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Reset();
        base.OnDetachedFromVisualTree(e);
    }

    private string _enteredString = "";

    private void TextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers == PlatformHelper.ControlKey && e.KeyModifiers == KeyModifiers.Shift)
            if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                Dispatcher.UIThread.Post(AutoFormat, DispatcherPriority.Background);

        if (e.Key == Key.V && e.KeyModifiers == PlatformHelper.ControlKey)
            if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                Dispatcher.UIThread.Post(() => _ = AutoFormatDelayAsync(), DispatcherPriority.Background);

        if (e.Key == Key.Back)
            if (_enteredString.Length > 0)
                _enteredString = _enteredString.Remove(_enteredString.Length - 1);
    }

    #endregion

    //-------------------------Quick Menu----------------------------------------------//

    #region Quick Menu

    /// <summary>
    ///     Sets event to handled if Cursor is over Selection
    /// </summary>
    private void PointerPressedBeforeCaretUpdate(object? sender, PointerEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        //Left Button
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
        }
        else if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            var pos = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)); //gets position of mouse

            if (pos.HasValue && !CodeBox.TextArea.Selection.IsEmpty) //Check if right click is on selection
            {
                var mouseOffset = CodeBox.Document.GetOffset(pos.Value.Line, pos.Value.Column);
                if (mouseOffset > 0 && mouseOffset < CodeBox.Document.TextLength)
                {
                    var selectionStartOffset = CodeBox.Document.GetOffset(
                        CodeBox.TextArea.Selection.StartPosition.Line,
                        CodeBox.TextArea.Selection.StartPosition.Column);
                    var selectionEndOffset = CodeBox.Document.GetOffset(CodeBox.TextArea.Selection.EndPosition.Line,
                        CodeBox.TextArea.Selection.EndPosition.Column);
                    if (selectionStartOffset > selectionEndOffset)
                        (selectionStartOffset, selectionEndOffset) = (selectionEndOffset, selectionStartOffset);

                    if (mouseOffset >= selectionStartOffset && mouseOffset <= selectionEndOffset) e.Handled = true;
                }
            }
        }
    }

    private async Task PointerPressedAfterCaretUpdateAsync(object? sender, PointerEventArgs e)
    {
        //Left Button
        if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            if (e.KeyModifiers == PlatformHelper.ControlKey)
                if (_controlAction != null)
                {
                    _controlAction.Invoke();
                    e.Handled = true;
                }

            //CodeBox.WordRenderer.SetHighlight(VhdpHelpers.SearchSelectedWord(CodeBox.Document, CodeBox.CaretOffset)); TODO
        }
        else if (e.GetCurrentPoint(null).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
        {
            var contextMenuList = new ObservableCollection<object>();

            // //Check for merge
            // foreach (var merge in CodeBox.MergeService.Merges)
            //     if (CodeBox.CaretOffset > merge.StartIndex && CodeBox.CaretOffset < merge.EndIndex)
            //         //contextMenuList.Add(new MenuItemViewModel
            //         //{
            //         //    Header = "Keep HEAD",
            //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepCurrent),
            //         //    CommandParameter = merge
            //         //});
            //         //contextMenuList.Add(new MenuItemViewModel
            //         //{
            //         //    Header = "Keep Incoming",
            //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepIncoming),
            //         //    CommandParameter = merge
            //         //});
            //         //contextMenuList.Add(new MenuItemViewModel
            //         //{
            //         //    Header = "Keep Both",
            //         //    Command = ReactiveCommand.Create<MergeEntry>(MergeService.MergeKeepBoth),
            //         //    CommandParameter = merge
            //         //});
            //         //contextMenuList.Add(new Separator());
            //
            //         break;

            if (_typeAssistance != null)
            {
                var languageSpecificItems = await _typeAssistance.GetQuickMenuAsync(CodeBox.CaretOffset);
                if (languageSpecificItems is not null && languageSpecificItems.Count > 0)
                {
                    contextMenuList.AddRange(languageSpecificItems);
                    contextMenuList.Add(new Separator());
                }
            }

            HoverBox.IsVisible = false;

            contextMenuList.Add(new MenuItemViewModel("Cut")
            {
                Header = "Cut",
                IconObservable = this.GetResourceObservable("BoxIcons.RegularCut"),
                Command = new RelayCommand(CodeBox.Cut)
            });
            contextMenuList.Add(new MenuItemViewModel("Copy")
            {
                Header = "Copy",
                IconObservable = this.GetResourceObservable("BoxIcons.RegularCopy"),
                Command = new RelayCommand(CodeBox.Copy)
            });
            contextMenuList.Add(new MenuItemViewModel("Paste")
            {
                Header = "Paste",
                IconObservable = this.GetResourceObservable("BoxIcons.RegularPaste"),
                Command = new RelayCommand(CodeBox.Paste)
            });
            if (_typeAssistance != null)
            {
                contextMenuList.Add(new Separator());
                contextMenuList.Add(new MenuItemViewModel("Comment")
                {
                    Header = "Comment",
                    IconObservable = this.GetResourceObservable("VsImageLib.CommentCode16X"),
                    Command = new RelayCommand(_typeAssistance.Comment)
                });
                contextMenuList.Add(new MenuItemViewModel("Uncomment")
                {
                    Header = "Uncomment",
                    IconObservable = this.GetResourceObservable("VsImageLib.UncommentCode16X"),
                    Command = new RelayCommand(_typeAssistance.Uncomment)
                });
            }

            if (!CodeBox.TextArea.Selection.IsEmpty)
                if (_typeAssistance != null)
                {
                    var startLine = CodeBox.TextArea.Selection.StartPosition.Line;
                    var endLine = CodeBox.TextArea.Selection.EndPosition.Line;
                    if (startLine > endLine) (startLine, endLine) = (endLine, startLine);

                    contextMenuList.Add(new Separator());
                    contextMenuList.Add(new MenuItemViewModel("IndentSelection")
                    {
                        Header = "Auto-Indent Selection",
                        IconObservable = this.GetResourceObservable("BoxIcons.RegularCode"),
                        Command = new RelayCommand(() => _typeAssistance.AutoIndent(startLine, endLine))
                    });
                }

            if (contextMenuList.Count > 0)
                CodeBox.TextArea.ContextMenu = new ContextMenu
                {
                    ItemsSource = contextMenuList,
                    Classes = { "BindMenu" }
                };
        }
    }

    //int clickOffset = -1;        

    private ErrorListItem? GetErrorAtMousePos(PointerEventArgs e)
    {
        if (ViewModel?.CurrentFile == null) return null;

        var pos = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)); //gets position of mouse
        if (pos.HasValue)
        {
            var offset = CodeBox.Document.GetOffset(pos.Value.Location);
            var location = CodeBox.Document.GetLocation(offset);
            foreach (var error in ContainerLocator.Container.Resolve<ErrorListViewModel>()
                         .GetErrorsForFile(ViewModel.CurrentFile))
                if (location.Line >= error.StartLine && location.Line <= error.EndLine &&
                    location.Column >= error.StartColumn && location.Column <= error.EndColumn)
                    return error;
        }

        return null;
    }

    #endregion

    //-------------------------Hover Info------------------------------------------------//

    #region Hover info

    private Action? _controlAction;
    private Range? _lastWordBounds;
    private PointerEventArgs? _lastMovedArgs;

    private void Pointer_Hover(object? sender, PointerEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        _ = TextEditorMouseHoverAsync(sender, e);
    }

    /// <summary>
    ///     closes toolTip if hover stopped
    /// </summary>
    private void Pointer_Moved(object? sender, PointerEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        var wordBounds = CodeBox.GetWordRangeAtPointerPosition(e);
        if (!wordBounds.Equals(_lastWordBounds))
        {
            HoverBox.Close();
            _controlAction = null;
            CodeBox.ModificationService.ClearModification("Control_Underline");
        }

        _lastMovedArgs = e;
        _lastWordBounds = wordBounds;

        if (e.KeyModifiers == PlatformHelper.ControlKey && _controlAction == null) _ = GetControlHoverActionAsync();

        if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
    }

    private void Pointer_Exited(object? sender, PointerEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        if (!HoverBox.IsPointerOverPopup) HoverBox.Close();
    }

    private async Task GetControlHoverActionAsync()
    {
        if (_lastMovedArgs == null) return;
        var word = CodeBox.GetWordAtPointerPosition(_lastMovedArgs);

        if (string.IsNullOrWhiteSpace(word)) return;

        if (Regex.IsMatch(word, @"\W")) return;

        if (_typeAssistance != null)
        {
            _controlAction =
                await _typeAssistance.GetActionOnControlWordAsync(
                    CodeBox.GetOffsetFromPointerPosition(_lastMovedArgs));

            if (_controlAction != null && _lastWordBounds != null)
            {
                CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");

                CodeBox.ModificationService.SetModification("Control_Underline", new TextModificationSegment(
                        _lastWordBounds.Value.Start.Value,
                        _lastWordBounds.Value.End.Value)
                    { Decorations = TextDecorationCollection.Parse("Underline") });
            }
        }
    }

    /// <summary>
    ///     opens toolTip if there is information about the word the mouse hovers over
    /// </summary>
    private async Task TextEditorMouseHoverAsync(object? sender, PointerEventArgs e)
    {
        if (!Equals(e.Source, CodeBox.TextArea.TextView)) return;

        if (!(TopLevel.GetTopLevel(this)?.IsKeyboardFocusWithin ?? false)) return;

        var offset = CodeBox.GetOffsetFromPointerPosition(e);

        var word = CodeBox.GetWordAtMousePos(e);

        if (offset <= 0 && HoverBox.IsOpen) return;

        //HoverTextBox.Markdown = "";
        HoverBoxContent.Content = null;

        if (word != null)
        {
            if (_typeAssistance != null)
            {
                var info = await _typeAssistance.GetHoverInfoAsync(offset);
                if (info != null && info.StartsWith("%object:")) //Show debugInfo
                {
                    var endInfo = info.IndexOf('%', 1);
                    var value = info[(endInfo + 1)..];
                    var name = info[8..endInfo];
                    var vm = ObjectValueModel.ParseValue(name, value);
                    if (vm != null)
                    {
                        vm.IsExpanded = true;
                        HoverBoxContent.Content = new VariableControlView
                        {
                            DataContext = new ObjectValueModel
                            {
                                Children = new ObservableCollection<ObjectValueModel> { vm },
                                DisplayName = name
                            }
                        };
                    }
                }
                else if (!string.IsNullOrWhiteSpace(info))
                {
                    var markdown = new MarkdownViewer
                    {
                        Markdown = info
                    };
                    HoverBoxContent.Content = markdown;
                }
            }

            if (HoverBoxContent.Content != null && !(CodeBox.TextArea.ContextMenu?.IsOpen ?? false))
            {
                UpdatePopupPositionToCursor(HoverBox, e);
                if (IsEffectivelyVisible) HoverBox.Open();

                e.Handled = true;
            }
            else
            {
                HoverBox.Close();
            }
        }
        else
        {
            HoverBox.Close();
        }
    }

    private void UpdatePopupPositionToCursor(Popup popup, PointerEventArgs e)
    {
        var textPosition = CodeBox.GetPositionFromPoint(e.GetPosition(CodeBox)) ?? new TextViewPosition(1, 1);
        var visualPosition = CodeBox.TextArea.TextView.GetVisualPosition(textPosition, VisualYPosition.LineBottom);
        visualPosition -= CodeBox.TextArea.TextView.ScrollOffset;
        popup.VerticalOffset = visualPosition.Y;
        popup.HorizontalOffset = visualPosition.X;
        popup.PlacementTarget = CodeBox.TextArea;
        popup.Placement = PlacementMode.AnchorAndGravity;
        popup.PlacementAnchor = PopupAnchor.TopLeft;
        popup.PlacementGravity = PopupGravity.BottomRight;
    }

    #endregion

    //-------------------------Type Assistance-------------------------------------------//

    #region Type Assistance

    private readonly IBrush _wordResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#502859af")!;
    private readonly IBrush _searchResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#50af7e28")!;

    /// <summary>
    ///     Fills in Data for the Completion Window
    /// </summary>
    private void TextEditor_TextArea_TextEntered(object? sender, TextInputEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        // //Apply Caret difference
        // if (CodeBox.CaretOffset + _caretDiff < 0) CodeBox.CaretOffset = 0;
        // else if (CodeBox.CaretOffset + _caretDiff > CodeBox.Text.Length) CodeBox.CaretOffset = CodeBox.Text.Length;
        // else CodeBox.CaretOffset += _caretDiff;
        // _caretDiff = 0;

        //Language Specific Type Assistance
        _typeAssistance?.TextEntered(e);

        // #region Detect Auto Format / Language Specific?
        //
        // _enteredString += e.Text;
        // var startOffset = -1;
        // if (e.Text == "}")
        //     startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("{", StringComparison.Ordinal);
        // else if (e.Text == ")")
        //     startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("(", StringComparison.Ordinal);
        // else if (_enteredString.Contains("#endregion"))
        //     startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("#region", StringComparison.Ordinal);
        // if (_enteredString.Length > 10) _enteredString = _enteredString.Remove(0, 1);
        //
        // if (startOffset >= 0)
        // {
        //     var startLineNumber = CodeBox.Document.GetLineByOffset(startOffset).LineNumber;
        //     var endLineNumber = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
        //     if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
        //         _typeAssistance?.AutoIndent(startLineNumber, endLineNumber);
        // }
        // //#endregion
    }

    private void TextEditor_TextArea_TextEntering(object? sender, TextInputEventArgs e)
    {
        if (ViewModel?.DisableEditViewEvents ?? true) return;

        if (e.Text is null) return;

        _typeAssistance?.TextEntering(e);
    }

    /// <summary>
    ///     Auto format entire document
    /// </summary>
    public void AutoFormat()
    {
        if (!CodeBox.IsReadOnly)
            _typeAssistance?.AutoIndent();
    }

    public async Task AutoFormatDelayAsync()
    {
        await Task.Delay(50);
        _typeAssistance?.AutoIndent();
    }

    #endregion
}