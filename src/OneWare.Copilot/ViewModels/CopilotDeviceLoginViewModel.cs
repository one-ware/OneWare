using System.Threading;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;

namespace OneWare.Copilot.ViewModels;

public sealed class CopilotDeviceLoginViewModel : FlexibleWindowViewModelBase
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    public CopilotDeviceLoginViewModel(CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
        Title = "Login to GitHub Copilot";
        Description = "Open the browser page and enter the code below.";
    }

    public IRelayCommand<Control?> CopyCodeCommand => new AsyncRelayCommand<Control?>(CopyToClipboardAsync);

    private async Task CopyToClipboardAsync(Control? owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.Clipboard == null) return;
        await topLevel.Clipboard.SetTextAsync(UserCode);
    }

    public string Description { get; }

    public string VerificationUrl
    {
        get;
        set => SetProperty(ref field, value);
    } = "https://github.com/login/device";

    public string UserCode
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string StatusText
    {
        get;
        set => SetProperty(ref field, value);
    } = "Starting Copilot login...";

    public void Cancel(FlexibleWindow window)
    {
        _cancellationTokenSource.Cancel();
        window.Close();
    }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        _cancellationTokenSource.Cancel();
        return true;
    }
}
