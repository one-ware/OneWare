using System.Reactive.Linq;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Settings.ViewModels;
using OneWare.Shared.Controls;

namespace OneWare.Settings.Views
{
    public partial class ApplicationSettingsView : FlexibleWindow
    {
        public ApplicationSettingsView()
        {
            InitializeComponent();
            
            TreeView.WhenValueChanged(x => x.SelectedItem)
                .Throttle(TimeSpan.FromMilliseconds(50)).Subscribe(x =>
            {
                if (x is SettingsCollectionViewModel)
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