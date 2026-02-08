using System.Collections.ObjectModel;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.ApplicationCommands.Models;
using OneWare.Essentials.Models;

namespace OneWare.ApplicationCommands.Tabs;

public abstract partial class CommandManagerTabBase : ObservableObject
{
    private readonly ILogical _logical;

    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private CommandManagerItemModel? _selectedItem;

    [ObservableProperty] private IList<CommandManagerItemModel> _visibleItems = [];

    protected CommandManagerTabBase(string title, ILogical logical)
    {
        _logical = logical;
        Title = title;

        this.WhenValueChanged(x => x.SearchText).Subscribe(_ => RefreshVisibleItems());
    }

    public string Title { get; }

    public ObservableCollection<IApplicationCommand> Items { get; init; } = new();

    public abstract string SearchBarText { get; }

    public void RefreshVisibleItems()
    {
        var query = SearchText ?? string.Empty;
        List<CommandManagerItemModel> newList = [];

        if (!string.IsNullOrWhiteSpace(query))
            newList = Items.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Select(c => new CommandManagerItemModel(c, c.CanExecute(_logical)))
                .OrderByDescending(c => c.IsEnabled)
                .ThenByDescending(c => c.Command.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(c => c.Command.Name)
                .ToList();

        VisibleItems = newList;
        SelectedItem = VisibleItems.FirstOrDefault();
    }
}
