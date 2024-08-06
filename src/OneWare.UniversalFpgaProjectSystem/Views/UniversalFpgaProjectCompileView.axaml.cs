using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Controls;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectCompileView : FlexibleWindow
{
    public UniversalFpgaProjectCompileView()
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
    }
}