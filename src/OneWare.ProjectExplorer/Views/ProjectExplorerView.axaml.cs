using Avalonia.Controls;
using Avalonia.Interactivity;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.ProjectExplorer.ViewModels;

namespace OneWare.ProjectExplorer.Views;

public partial class ProjectExplorerView : UserControl
{
    public ProjectExplorerView()
    {
        InitializeComponent();

        this.WhenValueChanged(x => x.DataContext).Subscribe(x =>
        {
            var vm = x as ProjectExplorerViewModel;
            if (vm == null) return;

            AddHandler(SearchBox.SearchEvent, (o, i) => { vm.OnSearch(); }, RoutingStrategies.Bubble);
        });

        TreeViewContextMenu.Opening += (sender, args) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null && DataContext is ProjectExplorerViewModel vm)
                vm.ConstructContextMenu(topLevel);
        };
    }
}