using System.Net;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.CloudIntegration.Services;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CloudIntegration.ViewModels;

public class AuthenticateCloudViewModel : FlexibleWindowViewModelBase
{
    private readonly ILogger _logger;
    private readonly OneWareCloudLoginService _loginService;
    private readonly ISettingsService _settingService;

    private readonly CancellationTokenSource _browserLoginCts = new();

    private string _email = string.Empty;

    private string? _errorText;

    private bool _isLoading;

    private string _password = string.Empty;

    public AuthenticateCloudViewModel(ISettingsService settingsService, ILogger logger,
        OneWareCloudLoginService loginService)
    {
        _settingService = settingsService;
        _logger = logger;
        _loginService = loginService;

        Title = "Login to OneWare Cloud";

        Description = "Login to OneWare Cloud";
    }

    public string Description { get; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string? ErrorText
    {
        get => _errorText;
        set => SetProperty(ref _errorText, value);
    }

    public bool IsWaitingForBrowserResponse
    {
        get;
        set => SetProperty(ref field, value);
    }

    public async Task LoginAsync(FlexibleWindow? window)
    {
        IsWaitingForBrowserResponse = true;
        ErrorText = null;

        var newListenerStarted = await _loginService.LoginAsync(_browserLoginCts.Token);
        if (newListenerStarted)
        {
            IsWaitingForBrowserResponse = false;
            window?.Close();

            ContainerLocator.Current.Resolve<IWindowService>().ActivateMainWindow();
            ContainerLocator.Current.Resolve<IMainDockService>()
                .Show(ContainerLocator.Current.Resolve<IOutputService>(), DockShowLocation.Bottom);
        }
    }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        _browserLoginCts.Cancel();
        return base.OnWindowClosing(window);
    }
}