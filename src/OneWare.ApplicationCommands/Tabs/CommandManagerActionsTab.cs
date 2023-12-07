using Avalonia.LogicalTree;
using OneWare.ApplicationCommands.ViewModels;

namespace OneWare.ApplicationCommands.Tabs;

public class CommandManagerActionsTab(ILogical logical) : CommandManagerTabBase("Actions", logical)
{
    public override string SearchBarText => $"{CommandManagerViewModel.ChangeShortcutGesture} to assign shortcut";
}