using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.LibraryExplorer.ViewModels;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.LibraryExplorer.Views;

public partial class LibraryExplorerView : UserControl
{
    public LibraryExplorerView()
    {
        InitializeComponent();

        this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
        {
            var vm = x as LibraryExplorerViewModel;
            if (vm == null) return;

            AddHandler(SearchBox.SearchEvent, (o, i) => { vm.OnSearch(); }, RoutingStrategies.Bubble);
        });

        TreeViewContextMenu.Opening += (sender, args) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && DataContext is LibraryExplorerViewModel vm)
                vm.ConstructContextMenu(topLevel);
        };
    }
}