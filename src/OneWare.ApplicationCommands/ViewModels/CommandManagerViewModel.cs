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
    public ILogical ActiveFocus { get; }
    public ObservableCollection<CommandManagerTabModel> Tabs { get; } = new();

    [ObservableProperty]
    private CommandManagerTabModel _selectedTab;

    public CommandManagerViewModel(ILogical logical, IApplicationCommandService commandService, IProjectExplorerService projectExplorerService)
    {
        ActiveFocus = logical;
        _applicationCommandService = commandService;
        _projectExplorerService = projectExplorerService;
        
        Tabs.Add(new CommandManagerTabModel("Files", logical)
        {
            Items = GetOpenFileCommands()
        });
        Tabs.Add(new CommandManagerTabModel("Symbols", logical));
        Tabs.Add(new CommandManagerTabModel("Actions", logical)
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
        if(SelectedTab?.SelectedItem?.Execute(ActiveFocus) ?? false)
            Close(window);
    }
}