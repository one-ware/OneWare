using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DynamicData.Binding;

namespace OneWare.Essentials.Controls
{
    /// <summary>
    ///     Interaction logic for SearchBox.xaml
    /// </summary>
    public partial class SearchBox : UserControl
    {
        public static readonly StyledProperty<bool> SearchButtonVisibleProperty =
            AvaloniaProperty.Register<SearchBox, bool>(nameof(Label), true, false, BindingMode.TwoWay);
        
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<SearchBox, string>(nameof(Label), "Search...", false, BindingMode.TwoWay);
        
        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<SearchBox, string>(nameof(SearchText), "", false, BindingMode.TwoWay);
        
        public static readonly RoutedEvent<RoutedEventArgs> SearchEvent =
            RoutedEvent.Register<SearchBox, RoutedEventArgs>(
                "TextSearch", RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public static readonly RoutedEvent<RoutedEventArgs> TextAddedEvent =
            RoutedEvent.Register<SearchBox, RoutedEventArgs>(
                "TextAdded", RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        public SearchBox()
        {
            InitializeComponent();

            SearchTextBox.WhenValueChanged(x => x.Text).Subscribe(_ => OnTextAddedEvent());

            KeyDown += SearchTextBox_KeyDown;
        }

        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }
        
        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool SearchButtonVisible
        {
            get => GetValue(SearchButtonVisibleProperty);
            set => SetValue(SearchButtonVisibleProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(SearchBox);

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!SearchTextBox.IsFocused)
                {
                    SearchTextBox.Focus();
                }
            });
          
            //SearchTextBox.SelectAll();
        }

        public void Reset()
        {
            SearchText = "";
            OnSearchEvent();
        }

        public void StartSearch()
        {
            OnSearchEvent();
        }

        // Allows add and remove of event handlers to handle the custom event
        public event EventHandler Search
        {
            add => AddHandler(SearchEvent, value);
            remove => RemoveHandler(SearchEvent, value);
        }

        public event EventHandler TextAdded
        {
            add => AddHandler(TextAddedEvent, value);
            remove => RemoveHandler(TextAddedEvent, value);
        }

        private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key.Equals(Key.Enter)) OnSearchEvent();
        }

        private void OnSearchEvent()
        {
            var newEventArgs = new RoutedEventArgs(SearchEvent);
            RaiseEvent(newEventArgs);
        }

        public void OnTextAddedEvent()
        {
            var newEventArgs = new RoutedEventArgs(TextAddedEvent);
            RaiseEvent(newEventArgs);
        }
    }
}