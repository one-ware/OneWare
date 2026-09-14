using Avalonia.Controls;
using Avalonia.Threading;

namespace OneWare.PackageManager.Views;

public partial class PackageView : UserControl
{
    public PackageView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Dispatcher.UIThread.Post(() => DetailsScroll.Offset = default, DispatcherPriority.Loaded);
    }
}