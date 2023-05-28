using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OneWare.SearchList.ViewModels;

namespace OneWare.SearchList.Views
{
    public partial class SearchListView : UserControl
    {
        public SearchListView()
        {
            InitializeComponent();

            Search.Search += (o, i) =>
            {
                if (DataContext is SearchListViewModel evm) evm.Search(Search.SearchText);
            };

            SearchList.AddHandler(DoubleTappedEvent, Open_SearchResult, RoutingStrategies.Bubble);

            KeyDown += (o, i) =>
            {
                if (i.Key == Key.Escape) (VisualRoot as Window)?.Close();
            };
        }
        
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Dispatcher.UIThread.Post(() =>
            {
                _ = FocusSearchBoxAsync();
            });
        }

        private async Task FocusSearchBoxAsync()
        {
            await Task.Delay(100);
            Search.Focus();
        }

        /// <summary>
        /// Double tapped event
        /// </summary>
        public void Open_SearchResult(object? sender, RoutedEventArgs e)
        {
            (DataContext as SearchListViewModel)?.GoToSearchResultAsync();
        }
    }
}