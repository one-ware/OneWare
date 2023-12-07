using Avalonia.LogicalTree;

namespace OneWare.ApplicationCommands.Tabs;

public class CommandManagerFilesTab(ILogical logical) : CommandManagerTabBase("Files", logical)
{
    public override string SearchBarText => $"";
}