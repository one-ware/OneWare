using Avalonia.Controls;
using OneWare.Copilot.Services;

namespace OneWare.Copilot.Views;

public partial class CopilotChatHeaderView : UserControl
{
    public CopilotChatHeaderView()
    {
        InitializeComponent();
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        if (DataContext is CopilotChatServiceBase { RefreshMcpServersCommand: { IsRunning: false } refresh })
            refresh.Execute(null);
    }
}
