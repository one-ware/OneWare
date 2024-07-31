using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.ApplicationCommands.Models;
using OneWare.Essentials.Commands;
using OneWare.Essentials.Models;

namespace OneWare.ApplicationCommands.Tabs;

public abstract partial class CommandManagerTabBase : ObservableObject
{
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private CommandManagerItemModel? _selectedItem;

    [ObservableProperty] private IList<CommandManagerItemModel> _visibleItems = [];

    protected CommandManagerTabBase(string title, ILogical logical)
    {
        Title = title;

        this.WhenValueChanged(x => x.SearchText).Subscribe(x =>
        {
            List<CommandManagerItemModel> newList = [];

            if (!string.IsNullOrWhiteSpace(x))
                newList = Items.Where(i => i.Name.Contains(x, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new CommandManagerItemModel(c, c.CanExecute(logical)))
                    .OrderByDescending(c => c.IsEnabled)
                    .ThenByDescending(c => c.Command.Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(c => c.Command.Name)
                    .ToList();

            VisibleItems = newList;
            SelectedItem = VisibleItems.FirstOrDefault();
        });
    }

    public string Title { get; }

    public ObservableCollection<IApplicationCommand> Items { get; init; } = new();

    public abstract string SearchBarText { get; }
}