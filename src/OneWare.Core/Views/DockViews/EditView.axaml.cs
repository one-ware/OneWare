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
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Search;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using Markdown.Avalonia;
using OneWare.Core.Data;
using OneWare.Core.Models;
using OneWare.Core.ViewModels.DockViews;
using Prism.Ioc;
using OneWare.Core.Extensions;
using OneWare.Core.Views.Controls;
using OneWare.ErrorList.ViewModels;
using OneWare.SDK.EditorExtensions;
using OneWare.SDK.Extensions;
using OneWare.SDK.Helpers;
using OneWare.SDK.LanguageService;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.Core.Views.DockViews
{
    public partial class EditView : UserControl
    {
        private readonly ISettingsService _settingsService;
        private readonly IErrorService _errorService;

        private CompositeDisposable _compositeDisposable = new();
        private ExtendedTextEditor CodeBox =>
            this.ViewModel?.Editor ?? throw new NullReferenceException(nameof(CodeBox));

        private ITypeAssistance? _typeAssistance;

        private IEnumerable<int> _lastSearchResultLines = new List<int>();
        public EditViewModel? ViewModel { get; set; }
        public ObjectValueModel? VaribleViewDataConext { get; private set; }

        public EditView()
        {
            _settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
            _errorService = ContainerLocator.Container.Resolve<IErrorService>();

            InitializeComponent();

            CompositeDisposable localComposite = new CompositeDisposable();
            
            this.DataContextChanged += (_, _) =>
            {
                localComposite.Dispose();
                localComposite = new CompositeDisposable();
                
                if (DataContext is not EditViewModel evm) return;
                ViewModel = evm;

                Observable.FromEventPattern(h => CodeBox.LayoutUpdated += h, h => CodeBox.LayoutUpdated -= h)
                    .Take(1)
                    .Subscribe((_) =>
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
                var completion = new CompletionWindow(CodeBox)
                {
                    CloseWhenCaretAtBeginning = false,
                    AdditionalOffset = new Vector(0, 3),
                    MaxHeight = 225
                };
                _typeAssistance?.Attach(completion);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(e.Message, e);
            }
            
            this.AddDisposableHandler(KeyDownEvent, (o, e) =>
            {
                if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl) return;
                if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
                _ = GetControlHoverActionAsync();
            }, RoutingStrategies.Bubble, true).DisposeWith(_compositeDisposable);

            this.AddDisposableHandler(KeyUpEvent, (o, e) =>
            {
                if (e.Key is Key.LeftCtrl or Key.RightCtrl)
                {
                    CodeBox.TextArea.TextView.Cursor = Cursor.Parse("IBeam");
                    CodeBox.ModificationService.ClearModification("Control_Underline");
                }
            }, RoutingStrategies.Bubble, true).DisposeWith(_compositeDisposable);
            
            //Zoom in ctrl + wheel
            CodeBox.AddDisposableHandler(PointerWheelChangedEvent, (o, i) =>
            {
                if (i.KeyModifiers != PlatformHelper.ControlKey) return;
                var fontSize = _settingsService.GetSettingValue<int>("Editor_FontSize");
                if (i.Delta.Y > 0) _settingsService.SetSettingValue("Editor_FontSize", fontSize + 1);
                else _settingsService.SetSettingValue("Editor_FontSize", fontSize - 1);
                ;
                i.Handled = true;
            }, RoutingStrategies.Tunnel, true).DisposeWith(_compositeDisposable);
        }
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
            CodeBox.RemoveHandler(KeyDownEvent, TextBox_KeyDown);

            CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedBeforeCaretUpdate);
            CodeBox.RemoveHandler(PointerPressedEvent, PointerPressedAfterCaretUpdate);

            if (CodeBox.SearchPanel != null)
                CodeBox.SearchPanel.OnSearch -= Search_Updated;
        }

        private void Search_Updated(object? sender, IEnumerable<SearchResult> results)
        {
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
            try
            {
                if (ViewModel == null) return;
                
                _lastSearchResultLines =
                    results.Select(x => CodeBox.Document.GetLineByOffset(x.StartOffset).LineNumber);

                ViewModel.ScrollInfo.Refresh("searchResult",_lastSearchResultLines
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
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
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
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
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
                        {
                            (selectionStartOffset, selectionEndOffset) = (selectionEndOffset, selectionStartOffset);
                        }

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
                    Header = "Cut", ImageIconObservable = this.GetResourceObservable("BoxIcons.RegularCut") ,
                    Command = new RelayCommand(CodeBox.Cut)
                });
                contextMenuList.Add(new MenuItemViewModel("Copy")
                {
                    Header = "Copy", ImageIconObservable = this.GetResourceObservable("BoxIcons.RegularCopy") ,
                    Command = new RelayCommand(CodeBox.Copy)
                });
                contextMenuList.Add(new MenuItemViewModel("Paste")
                {
                    Header = "Paste", ImageIconObservable = this.GetResourceObservable("BoxIcons.RegularPaste") ,
                    Command = new RelayCommand(CodeBox.Paste)
                });
                if (_typeAssistance != null)
                {
                    contextMenuList.Add(new Separator());
                    contextMenuList.Add(new MenuItemViewModel("Comment")
                    {
                        Header = "Comment", ImageIconObservable = this.GetResourceObservable("VsImageLib.CommentCode16X") ,
                        Command = new RelayCommand(_typeAssistance.Comment)
                    });
                    contextMenuList.Add(new MenuItemViewModel("Uncomment")
                    {
                        Header = "Uncomment", ImageIconObservable = this.GetResourceObservable("VsImageLib.UncommentCode16X") ,
                        Command = new RelayCommand(_typeAssistance.Uncomment)
                    });
                }

                if (!CodeBox.TextArea.Selection.IsEmpty)
                {
                    if (_typeAssistance != null)
                    {
                        var startLine = CodeBox.TextArea.Selection.StartPosition.Line;
                        var endLine = CodeBox.TextArea.Selection.EndPosition.Line;
                        if (startLine > endLine)
                        {
                            (startLine, endLine) = (endLine, startLine);
                        }
                        contextMenuList.Add(new Separator());
                        contextMenuList.Add(new MenuItemViewModel("IndentSelection")
                        {
                            Header = "Auto-Indent Selection",
                            ImageIconObservable = this.GetResourceObservable("BoxIcons.RegularCode") ,
                            Command = new RelayCommand(() => _typeAssistance.AutoIndent(startLine, endLine))
                        });
                    }
                       
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

        private void Pointer_Hover(object? sender, PointerEventArgs e)
        {
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
            _ = TextEditorMouseHoverAsync(sender, e);
        }

        /// <summary>
        ///     closes toolTip if hover stopped
        /// </summary>
        private void Pointer_Moved(object? sender, PointerEventArgs e)
        {
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
            var word = CodeBox.GetWordAtMousePos(e);
            if (word == "" || word != _lastHoverWord) HoverBox.Close();

            _lastMovedArgs = e;

            if (e.KeyModifiers == PlatformHelper.ControlKey) _ = GetControlHoverActionAsync();
        }

        private Action? _controlAction;
        private string? _lastHoverWord;
        private Range? _lastControlWordBounds;
        private PointerEventArgs? _lastMovedArgs;

        public async Task GetControlHoverActionAsync()
        {
            if (_lastMovedArgs == null) return;
            var wordBounds = CodeBox.GetWordRangeAtPointerPosition(_lastMovedArgs);
            var word = CodeBox.GetWordAtPointerPosition(_lastMovedArgs);

            if (word == null) return;

            //if (!word.All(Char.IsLetterOrDigit)) return;
            if (Regex.IsMatch(word, @"\W")) return;

            if (string.IsNullOrWhiteSpace(word))
            {
                _controlAction = null;
                _lastControlWordBounds = null;
            }

            if (_typeAssistance != null && !string.IsNullOrWhiteSpace(word) && wordBounds.HasValue &&
                (_lastControlWordBounds == null || !wordBounds.Value.Equals(_lastControlWordBounds.Value)))
            {
                _lastControlWordBounds = wordBounds;
                _controlAction =
                    await _typeAssistance.GetActionOnControlWordAsync(
                        CodeBox.GetOffsetFromPointerPosition(_lastMovedArgs));
                if (_controlAction != null) CodeBox.TextArea.TextView.Cursor = Cursor.Parse("Hand");
            }

            if (_controlAction != null && _lastControlWordBounds.HasValue)
            {
                CodeBox.ModificationService.SetModification("Control_Underline", new TextModificationSegment(
                    _lastControlWordBounds.Value.Start.Value,
                    _lastControlWordBounds.Value.End.Value) { Decorations = TextDecorationCollection.Parse("Underline") });
            }
        }

        /// <summary>
        ///     opens toolTip if there is information about the word the mouse hovers over
        /// </summary>
        private async Task TextEditorMouseHoverAsync(object? sender, PointerEventArgs e)
        {
            if (!Equals(e.Source, CodeBox.TextArea.TextView)) return;
            
            var offset = CodeBox.GetOffsetFromPointerPosition(e);

            var word = CodeBox.GetWordAtMousePos(e);

            if (offset <= 0 || word == _lastHoverWord && HoverBox.IsOpen) return;

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
                            HoverBoxContent.Content = new VariableControlView()
                            {
                                DataContext = new ObjectValueModel
                                {
                                    Children = new ObservableCollection<ObjectValueModel> {vm},
                                    DisplayName = name
                                }
                            };
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(info))
                    {
                        var markdown = new MarkdownScrollViewer()
                        {
                            Markdown = info,
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

            _lastHoverWord = word;
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

        //-------------------------Highlighting----------------------------------------------//

        #region Highlighting
        

        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            CodeBox.TextArea.ContextMenu?.Close();
            HoverBox.Close();
            
            if(ViewModel?.DisableEditViewEvents ?? true) return;

            var searcher = new CBracketSearcher();
            CodeBox.BracketRenderer.SetHighlight(searcher.SearchBracket(CodeBox.Document, CodeBox.CaretOffset));
        }

        #endregion

        //-------------------------Type Assistance-------------------------------------------//

        #region Type Assistance

        private readonly IBrush _wordResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#502859af")!;
        private readonly IBrush _searchResultScrollBrush = (IBrush)new BrushConverter().ConvertFrom("#50af7e28")!;

        /// <summary>
        /// Fills in Data for the Completion Window
        /// </summary>
        private void TextEditor_TextArea_TextEntered(object? sender, TextInputEventArgs e)
        {
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
            //Apply Caret difference
            if (CodeBox.CaretOffset + _caretDiff < 0) CodeBox.CaretOffset = 0;
            else if (CodeBox.CaretOffset + _caretDiff > CodeBox.Text.Length) CodeBox.CaretOffset = CodeBox.Text.Length;
            else CodeBox.CaretOffset += _caretDiff;
            _caretDiff = 0;

            //Language Specific Type Assistance
            _typeAssistance?.TextEntered(e);

            #region Detect Auto Format / Language Specific?

            _enteredString += e.Text;
            var startOffset = -1;
            if (e.Text == "}")
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("{", StringComparison.Ordinal);
            else if (e.Text == ")")
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("(", StringComparison.Ordinal);
            else if (_enteredString.Contains("#endregion"))
                startOffset = CodeBox.Text[..CodeBox.CaretOffset].LastIndexOf("#region", StringComparison.Ordinal);
            if (_enteredString.Length > 10) _enteredString = _enteredString.Remove(0, 1);

            if (startOffset >= 0)
            {
                var startLineNumber = CodeBox.Document.GetLineByOffset(startOffset).LineNumber;
                var endLineNumber = CodeBox.Document.GetLineByOffset(CodeBox.CaretOffset).LineNumber;
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoFormatting"))
                    _typeAssistance?.AutoIndent(startLineNumber, endLineNumber);
            }

            #endregion
        }

        //Difference between TextEntering and TextEntered
        private int _caretDiff;

        private void TextEditor_TextArea_TextEntering(object? sender, TextInputEventArgs e)
        {
            if(ViewModel?.DisableEditViewEvents ?? true) return;
            
            if (e.Text is null) return;
            
            if (CodeBox.CaretOffset > 1 && CodeBox.CaretOffset <= CodeBox.Text.Length)
                if (_settingsService.GetSettingValue<bool>("Editor_UseAutoBracket"))
                {
                    if (e.Text[0] == '(')
                    {
                        e.Text += ')';
                        _caretDiff = -1;
                    }
                    else if (e.Text[0] == '{')
                    {
                        e.Text += '}';
                        _caretDiff = -1;
                    }
                    else if (e.Text[0] == ')' && CodeBox.CaretOffset > 1 &&
                             CodeBox.CaretOffset < CodeBox.Document.TextLength &&
                             CodeBox.Text[CodeBox.CaretOffset] == ')')
                    {
                        var br = 0;
                        for (var i = CodeBox.CaretOffset - 1; i >= 0; i--)
                        {
                            if (CodeBox.Text[i] == '{') break;
                            if (CodeBox.Text[i] == '(') br++;
                            if (CodeBox.Text[i] == ')') br--;
                        }

                        if (br > 0)
                        {
                            e.Text = "";
                            _caretDiff = 1;
                            return; //Dont continue without text           
                        }
                    }
                    else if (e.Text[0] == '}' && CodeBox.CaretOffset > 1 &&
                             CodeBox.Text[CodeBox.CaretOffset - 1] == '{')
                    {
                        e.Text = "";
                        _caretDiff = 1;
                        return; //Dont continue without text
                    }
                }

            //Language Specific TypeAssistance
            _typeAssistance?.TextEntering(e);
        }

        /// <summary>
        /// Auto format entire document
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
}