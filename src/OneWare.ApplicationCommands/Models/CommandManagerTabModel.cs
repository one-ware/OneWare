using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.LogicalTree;
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

    [ObservableProperty]
    private IList<CommandManagerItemModel> _visibleItems = [];

    [ObservableProperty]
    private CommandManagerItemModel? _selectedItem;

    public CommandManagerTabModel(string title, ILogical logical)
    {
        Title = title;

        this.WhenValueChanged(x => x.SearchText).Subscribe(x =>
        {
            List<CommandManagerItemModel> newList = [];
            
            if (!string.IsNullOrWhiteSpace(x))
            {
                newList = Items.Where(i => i.Name.Contains(x, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new CommandManagerItemModel(c, c.CanExecute(logical)))
                    .OrderByDescending(c => c.IsEnabled)
                    .ThenByDescending(c => c.Command.Name.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(c => c.Command.Name)
                    .ToList();
            }
            if (newList.Count == 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                newList.Add(new CommandManagerItemModel(new DummyApplicationCommand(), false));
            }

            VisibleItems = newList;
            SelectedItem = VisibleItems.FirstOrDefault();
        });
    }
}