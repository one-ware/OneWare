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
            
            KeyDown += (o, i) =>
            {
                if (i.Key == Key.Escape) (VisualRoot as Window)?.Close();
            };
        }
    }
}