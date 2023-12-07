using Avalonia.LogicalTree;

namespace OneWare.ApplicationCommands.Tabs;

public class CommandManagerAllTab(ILogical logical) : CommandManagerTabBase("All", logical)
{
    public override string SearchBarText => "";
}