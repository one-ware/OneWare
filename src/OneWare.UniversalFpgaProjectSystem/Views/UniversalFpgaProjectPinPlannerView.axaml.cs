using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectPinPlannerView : FlexibleWindow
{
    public UniversalFpgaProjectPinPlannerView()
    {
        InitializeComponent();

        VisiblePinDataGrid.SelectionChanged += (_, _) =>
        {
            if (VisiblePinDataGrid.SelectedItem != null)
                VisiblePinDataGrid.ScrollIntoView(VisiblePinDataGrid.SelectedItem, null);
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