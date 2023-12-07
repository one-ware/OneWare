using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.ApplicationCommands.Models;
using OneWare.SDK.Commands;
using OneWare.SDK.Models;

namespace OneWare.ApplicationCommands.Tabs;

public abstract partial class CommandManagerTabBase : ObservableObject
{
    public string Title { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<IApplicationCommand> Items { get; init; } = new();

    [ObservableProperty]
    private IList<CommandManagerItemModel> _visibleItems = [];

    [ObservableProperty]
    private CommandManagerItemModel? _selectedItem;
    
    public abstract string SearchBarText { get; }

    protected CommandManagerTabBase(string title, ILogical logical)
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
            //Add empty ListItem for Linux to avoid crash, TODO check with next Avalonia Update
            if (newList.Count == 0 && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                newList.Add(new CommandManagerItemModel(new DummyApplicationCommand(), false));
            }

            VisibleItems = newList;
            SelectedItem = VisibleItems.FirstOrDefault();
        });
    }
}