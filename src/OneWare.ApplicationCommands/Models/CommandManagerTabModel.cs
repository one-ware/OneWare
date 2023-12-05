using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.SDK.Models;

namespace OneWare.ApplicationCommands.Models;

public partial class CommandManagerTabModel : ObservableObject
{
    public string Title { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<IApplicationCommand> Items { get; init; } = new();

    public ObservableCollection<IApplicationCommand> VisibleItems { get; } = new();

    [ObservableProperty]
    private IApplicationCommand? _selectedItem;

    public CommandManagerTabModel(string title)
    {
        Title = title;

        this.WhenValueChanged(x => x.SearchText).Subscribe(x =>
        {
            if(x is null) return;
            VisibleItems.Clear();
            VisibleItems.AddRange(Items.Where(i => i.Name.Contains(x, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => !c.Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                .ThenBy(c => c.Name));
            SelectedItem = VisibleItems.FirstOrDefault();
        });
    }
}