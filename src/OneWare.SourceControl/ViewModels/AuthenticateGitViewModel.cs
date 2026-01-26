using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.LoginProviders;

namespace OneWare.SourceControl.ViewModels;

public class AuthenticateGitViewModel : FlexibleWindowViewModelBase
{
    private readonly ILoginProvider _loginProvider;

    private bool _isLoading = true;

    private string _password = string.Empty;

    private string _server;

    public AuthenticateGitViewModel(ILoginProvider loginProvider)
    {
        Title = $"Login to {loginProvider.Name}";

        _loginProvider = loginProvider;
        _server = loginProvider.Host;

        Description = $"Login to {loginProvider.Name} using Auth token";
    }

    public string Description { get; }

    public bool Success { get; private set; }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Server
    {
        get => _server;
        set => SetProperty(ref _server, value);
    }

    public void Generate()
    {
        PlatformHelper.OpenHyperLink(_loginProvider.GenerateLink);
    }

    public async Task LoginAsync(FlexibleWindow window)
    {
        if (string.IsNullOrWhiteSpace(Password)) return;

        var result = await _loginProvider.LoginAsync(Password);

        if (!result) return;

        Success = true;

        window.Close();
    }

    public void Cancel(FlexibleWindow window)
    {
        window.Close();
    }
}