using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.ApplicationCommands.Models;
using OneWare.ApplicationCommands.Services;
using OneWare.SDK.ViewModels;

namespace OneWare.ApplicationCommands.ViewModels;

public partial class CommandManagerViewModel : FlexibleWindowViewModelBase
{
    private ApplicationCommandService _applicationCommandService;
    public ObservableCollection<CommandManagerTabModel> Tabs { get; } = new();

    [ObservableProperty]
    private CommandManagerTabModel _selectedTab;

    public CommandManagerViewModel(ApplicationCommandService commandService)
    {
        _applicationCommandService = commandService;
        Tabs.Add(new CommandManagerTabModel("Files"));
        Tabs.Add(new CommandManagerTabModel("Symbols"));
        Tabs.Add(new CommandManagerTabModel("Actions"));

        _selectedTab = Tabs.Last();
    }
}