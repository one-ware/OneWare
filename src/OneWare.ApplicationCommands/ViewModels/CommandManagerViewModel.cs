using System.Collections.ObjectModel;
using Avalonia.LogicalTree;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.Services;
using OneWare.SDK.Controls;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.ApplicationCommands.ViewModels;

public partial class CommandManagerViewModel : FlexibleWindowViewModelBase
{
    private readonly IApplicationCommandService _applicationCommandService;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly ILogical _logical;
    public ObservableCollection<CommandManagerTabModel> Tabs { get; } = new();

    [ObservableProperty]
    private CommandManagerTabModel _selectedTab;

    public CommandManagerViewModel(ILogical logical, IApplicationCommandService commandService, IProjectExplorerService projectExplorerService)
    {
        _logical = logical;
        _applicationCommandService = commandService;
        _projectExplorerService = projectExplorerService;
        
        Tabs.Add(new CommandManagerTabModel("Files")
        {
            Items = GetOpenFileCommands()
        });
        Tabs.Add(new CommandManagerTabModel("Symbols"));
        Tabs.Add(new CommandManagerTabModel("Actions")
        {
            Items = commandService.ApplicationCommands
        });

        _selectedTab = Tabs.Last();
    }

    private ObservableCollection<IApplicationCommand> GetOpenFileCommands()
    {
        var collection = new ObservableCollection<IApplicationCommand>();
        foreach (var entry in _projectExplorerService.Items)
        {
            switch (entry)
            {
                case IProjectRoot root:
                    collection.AddRange(root.Files.Select(x => new OpenFileApplicationCommand(x)));
                    break;
                case IProjectFile file:
                    collection.Add(new OpenFileApplicationCommand(file));
                    break;
                case IProjectFolder folder:
                    //Cant happen atm
                    break;
            }   
        }
        return collection;
    }

    public void ExecuteSelection(FlexibleWindow window)
    {
        SelectedTab?.SelectedItem?.Execute(_logical);
        Close(window);
    }
}