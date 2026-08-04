using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.ViewModels;
using OneWare.SourceControl.LoginProviders;

namespace OneWare.SourceControl.ViewModels;

public class AuthenticateGitViewModel : DeviceCodeLoginViewModel
{
    private readonly ILoginProvider _loginProvider;

    private string _password = string.Empty;

    private string _server;

    private bool _showTokenLogin;

    public AuthenticateGitViewModel(ILoginProvider loginProvider)
        : base($"Login to {loginProvider.Name}", GetDescription(loginProvider),
            (prompt, token) => RunDeviceCodeLoginAsync(loginProvider, prompt, token))
    {
        _loginProvider = loginProvider;
        _server = loginProvider.Host;

        IsDeviceCodeLoginEnabled = IsDeviceCodeLoginAvailable(loginProvider);
        _showTokenLogin = !IsDeviceCodeLoginEnabled;
    }

    /// <summary>
    ///     True when the manual personal access token fallback is shown.
    /// </summary>
    public bool ShowTokenLogin
    {
        get => _showTokenLogin;
        set => SetProperty(ref _showTokenLogin, value);
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

    public void ShowTokenLoginFallback()
    {
        ShowTokenLogin = true;
    }

    public void Generate()
    {
        PlatformHelper.OpenHyperLink(_loginProvider.GenerateLink);
    }

    public async Task LoginAsync(FlexibleWindow window)
    {
        if (string.IsNullOrWhiteSpace(Password)) return;

        Status = "Verifying token...";

        if (!await _loginProvider.LoginAsync(Password))
        {
            Status = $"The token was rejected by {_loginProvider.Name}.";
            return;
        }

        MarkSuccess();

        window.Close();
    }

    protected override void OnLoginFailed()
    {
        ShowTokenLogin = true;
    }

    private static bool IsDeviceCodeLoginAvailable(ILoginProvider loginProvider)
    {
        return loginProvider is IDeviceCodeLoginProvider { IsDeviceCodeLoginAvailable: true };
    }

    private static string GetDescription(ILoginProvider loginProvider)
    {
        return IsDeviceCodeLoginAvailable(loginProvider)
            ? $"Open the browser page and enter the code below to authorize OneWare Studio on {loginProvider.Name}."
            : $"Login to {loginProvider.Name} using Auth token";
    }

    private static async Task<bool> RunDeviceCodeLoginAsync(ILoginProvider loginProvider,
        IDeviceCodeLoginPrompt prompt, CancellationToken cancellationToken)
    {
        if (loginProvider is not IDeviceCodeLoginProvider { IsDeviceCodeLoginAvailable: true } provider) return false;

        prompt.Status = "Requesting device code...";

        var start = await provider.StartDeviceCodeLoginAsync(cancellationToken);

        if (start.DeviceCode is not { } deviceCode)
        {
            prompt.Status = start.ErrorMessage ?? "Could not start the login.";
            return false;
        }

        prompt.UserCode = deviceCode.UserCode;
        prompt.VerificationUrl = deviceCode.VerificationUri;
        prompt.Status = "Waiting for authorization in the browser...";

        PlatformHelper.OpenHyperLink(deviceCode.VerificationUri);

        var result = await provider.CompleteDeviceCodeLoginAsync(deviceCode, cancellationToken);

        if (result.Status == DeviceCodeLoginStatus.Cancelled) return false;

        if (!result.IsSuccess)
        {
            prompt.Status = result.ErrorMessage ?? "Login failed.";
            return false;
        }

        prompt.Status = $"Logged in as {result.Username}.";
        return true;
    }
}
