using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace OneWare.Debugger.Views;

public partial class EditableValueCell : UserControl
{
    public EditableValueCell()
    {
        InitializeComponent();

        EditBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !EditBox.IsVisible) return;

            Dispatcher.UIThread.Post(() =>
            {
                EditBox.Focus();
                EditBox.SelectAll();
            });
        };
    }
}
