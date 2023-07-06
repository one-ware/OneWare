using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using OneWare.Shared;
using NotifyPropertyChangedEx = DynamicData.Binding.NotifyPropertyChangedEx;

namespace OneWare.PackageManager.Views
{
    public partial class PackageManagerView : FlexibleWindow
    {
        public PackageManagerView()
        {
            InitializeComponent();
        }
    }
}