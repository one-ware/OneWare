using Avalonia.LogicalTree;

namespace OneWare.ApplicationCommands.Tabs;

public class CommandManagerSymbolsTab(ILogical logical) : CommandManagerTabBase("Symbols", logical)
{
    public override string SearchBarText => "";
}
