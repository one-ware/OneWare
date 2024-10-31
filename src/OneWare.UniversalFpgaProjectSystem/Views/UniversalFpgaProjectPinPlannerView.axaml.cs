using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Controls;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectPinPlannerView : FlexibleWindow
{
    public UniversalFpgaProjectPinPlannerView()
    {
        InitializeComponent();

        VisiblePinDataGrid.SelectionChanged += (_, _) =>
        {
            if (VisiblePinDataGrid.SelectedItem != null)
            {
                VisiblePinDataGrid.ScrollIntoView(VisiblePinDataGrid.SelectedItem, null);
            }
        };
        
        VisiblePinDataGrid.SelectionChanged += (_, _) =>
        {
            if (VisiblePinDataGrid.SelectedItem != null)
            {
                VisiblePinDataGrid.ScrollIntoView(VisiblePinDataGrid.SelectedItem, null);
            }
        };

        ZoomContentControl.WhenValueChanged(x => x.Content).Subscribe(x =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                //ZoomBorder.Fill();
            });
        });
    }
}