using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace OneWare.Essentials.EditorExtensions
{
    public class TextInputWindow : Popup
    {
        private readonly Window _parentWindow;

        private readonly TextInputControl _textInputControl;

        private Point _visualLocation;
        private Point _visualLocationTop;

        /// <summary>
        ///     Creates a new CompletionWindowBase.
        /// </summary>
        public TextInputWindow(TextArea textArea, TextViewPosition pos, string initialValue)
        {
            _textInputControl = new TextInputControl(initialValue);
            Child = _textInputControl;

            TextArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
            _parentWindow = textArea.GetVisualRoot() as Window ?? throw new NullReferenceException("No VisualRoot");

            PlacementGravity = PopupGravity.BottomRight;
            PlacementAnchor = PopupAnchor.TopLeft;

            Closed += (sender, args) => DetachEvents();

            AttachEvents();

            SetPosition(pos);
        }

        /// <summary>
        ///     Gets the parent TextArea.
        /// </summary>
        public TextArea TextArea { get; }

        public Action<string?>? CompleteAction { get; set; }

        /// <summary>
        ///     Gets whether the completion window should automatically close when the text editor loses focus.
        /// </summary>
        protected virtual bool CloseOnFocusLost => true;

        public Vector AdditionalOffset { get; set; } = Vector.Zero;
        public VisualYPosition VisualPosition { get; set; } = VisualYPosition.LineBottom;

        protected override Type StyleKeyOverride => typeof(Popup);

        protected virtual void OnClosed()
        {
            DetachEvents();
        }

        public void Show()
        {
            Height = double.NaN;
            MinHeight = 0;

            UpdatePosition();
            Open();
        }

        public void Hide()
        {
            Close();
            OnClosed();
        }

        private void CloseIfFocusLost()
        {
            if (CloseOnFocusLost) Hide();
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == Key.Escape) TextArea.Focus();
            if (!e.Handled && e.Key == Key.Enter)
            {
                CompleteAction?.Invoke(_textInputControl.Input?.Text ?? null);
                TextArea.Focus();
            }
        }


        /// <summary>
        ///     Positions the completion window at the specified position.
        /// </summary>
        protected void SetPosition(TextViewPosition position)
        {
            var textView = TextArea.TextView;

            _visualLocation = textView.GetVisualPosition(position, VisualPosition);
            _visualLocationTop = textView.GetVisualPosition(position, VisualYPosition.LineTop);

            UpdatePosition();
        }

        /// <summary>
        ///     Updates the position of the CompletionWindow based on the parent TextView position and the screen working area.
        ///     It ensures that the CompletionWindow is completely visible on the screen.
        /// </summary>
        protected void UpdatePosition()
        {
            var textView = TextArea.TextView;

            var position = _visualLocation - textView.ScrollOffset + AdditionalOffset;

            PlacementTarget = textView;
            Placement = PlacementMode.AnchorAndGravity;
            HorizontalOffset = position.X;
            VerticalOffset = position.Y;
        }

        #region Event Handlers

        private void AttachEvents()
        {
            ((ISetLogicalParent)this).SetParent(TextArea.GetVisualRoot() as ILogical);

            LostFocus += Focus_Lost;
            TextArea.TextView.ScrollOffsetChanged += TextViewScrollOffsetChanged;
            if (_parentWindow != null)
            {
                _parentWindow.Deactivated += ParentWindow_Deactivated;
            }
        }

        /// <summary>
        ///     Detaches events from the text area.
        /// </summary>
        protected virtual void DetachEvents()
        {
            ((ISetLogicalParent)this).SetParent(null);

            LostFocus -= Focus_Lost;
            TextArea.TextView.ScrollOffsetChanged -= TextViewScrollOffsetChanged;
            if (_parentWindow != null)
            {
                _parentWindow.Deactivated -= ParentWindow_Deactivated;
            }
        }

        private void TextViewScrollOffsetChanged(object? sender, EventArgs e)
        {
            ILogicalScrollable textView = TextArea;
            var visibleRect = new Rect(textView.Offset.X, textView.Offset.Y, textView.Viewport.Width,
                textView.Viewport.Height);
            //close completion window when the user scrolls so far that the anchor position is leaving the visible area
            if (visibleRect.Contains(_visualLocation) || visibleRect.Contains(_visualLocationTop))
                UpdatePosition();
            else
                Hide();
        }

        private void Focus_Lost(object? sender, RoutedEventArgs e)
        {
            Dispatcher.UIThread.Post(CloseIfFocusLost, DispatcherPriority.Background);
        }

        private void ParentWindow_Deactivated(object? sender, EventArgs e)
        {
            Hide();
        }
        
        /// <inheritdoc />
        private void OnDeactivated(object? sender, EventArgs e)
        {
            Dispatcher.UIThread.Post(CloseIfFocusLost, DispatcherPriority.Background);
        }

        #endregion
    }
}