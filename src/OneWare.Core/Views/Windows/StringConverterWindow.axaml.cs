using Avalonia;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.Windows;
using OneWare.Shared;

namespace OneWare.Core.Views.Windows
{
    public partial class StringConverterWindow : AdvancedWindow
    {
        public StringConverterWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            DataContext = new StringConverterWindowViewModel();
            
            ResultBox.SyntaxHighlighting = SourceBox.SyntaxHighlighting;
        }


    }
}