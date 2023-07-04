using System.Reactive.Linq;
using Avalonia;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Settings.Models;
using OneWare.Shared;

namespace OneWare.Settings.Views
{
    public partial class SettingsView : FlexibleWindow
    {
        public SettingsView()
        {
            InitializeComponent();
            
            TreeView.WhenValueChanged(x => x.SelectedItem)
                .Throttle(TimeSpan.FromMilliseconds(50)).Subscribe(x =>
            {
                if (x is SubCategoryModel)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        SettingsList.ScrollIntoView(x);
                        SettingsList.ContainerFromItem(x)?.Classes.Remove("Highlight");
                        SettingsList.ContainerFromItem(x)?.Classes.Add("Highlight");
                    });
                }
            });
        }
    }
}