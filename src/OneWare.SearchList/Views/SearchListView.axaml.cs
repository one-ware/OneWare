using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    }
}