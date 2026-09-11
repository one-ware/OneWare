using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.ViewModels;

/// <summary>
///     The subset of the device login dialog a login implementation is allowed to write to.
///     All members are safe to set from any thread.
/// </summary>
public interface IDeviceCodeLoginPrompt
{
    /// <summary>
    ///     The code the user has to enter on the verification page.
    /// </summary>
    public string UserCode { get; set; }

    /// <summary>
    ///     The page the user has to open to enter <see cref="UserCode" />.
    /// </summary>
    public string VerificationUrl { get; set; }

    /// <summary>
    ///     Human readable progress or error information.
    /// </summary>
    public string Status { get; set; }
}

/// <summary>
///     Shared dialog view model for device code style logins (OAuth device authorization grant,
///     CLI driven logins, ...). The actual login is supplied as a delegate, so this view model stays
///     independent of the transport used to obtain the code.
/// </summary>
public class DeviceCodeLoginViewModel : FlexibleWindowViewModelBase, IDeviceCodeLoginPrompt
{
    private const int MaxDisplayedUrlLength = 60;

    /// <summary>
    ///     Performs the login and reports progress through <paramref name="prompt" />.
    ///     Returns true when the user was logged in successfully.
    /// </summary>
    public delegate Task<bool> DeviceCodeLoginAction(IDeviceCodeLoginPrompt prompt,
        CancellationToken cancellationToken);

    private readonly DeviceCodeLoginAction _loginAction;

    private CancellationTokenSource? _cancellationTokenSource;

    private bool _isBusy;

    private bool _loginStarted;

    private string _status = string.Empty;

    private string _userCode = string.Empty;

    private string _verificationUrl = string.Empty;

    public DeviceCodeLoginViewModel(string title, string description, DeviceCodeLoginAction loginAction)
    {
        Title = title;
        Description = description;
        _loginAction = loginAction;
    }

    public string Description { get; }

    /// <summary>
    ///     True once the login completed successfully.
    /// </summary>
    public bool Success { get; private set; }

    /// <summary>
    ///     Set to false to keep the dialog from starting the login automatically.
    /// </summary>
    public bool IsDeviceCodeLoginEnabled { get; protected set; } = true;

    public IRelayCommand<Control?> CopyCodeCommand => new AsyncRelayCommand<Control?>(CopyCodeAsync);

    public IRelayCommand RetryCommand => new AsyncRelayCommand<FlexibleWindow?>(RunLoginAsync);

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetUiProperty(ref _isBusy, value);
    }

    public string Status
    {
        get => _status;
        set => SetUiProperty(ref _status, value);
    }

    public string UserCode
    {
        get => _userCode;
        set => SetUiProperty(ref _userCode, value);
    }

    public string VerificationUrl
    {
        get => _verificationUrl;
        set
        {
            SetUiProperty(ref _verificationUrl, value);
            OnPropertyChangedFromAnyThread(nameof(VerificationUrlLabel));
        }
    }

    /// <summary>
    ///     Display text for <see cref="VerificationUrl" />. Browser based flows use long authorization URLs that
    ///     would blow up the dialog layout, so those are shown as a short label instead.
    /// </summary>
    public string VerificationUrlLabel =>
        VerificationUrl.Length > MaxDisplayedUrlLength ? "Open the sign-in page" : VerificationUrl;

    public override void OnWindowOpened(FlexibleWindow window)
    {
        TryStartLogin(window);
    }

    public override bool OnWindowClosing(FlexibleWindow window)
    {
        CancelPendingLogin();
        return true;
    }

    /// <summary>
    ///     Starts the login exactly once, no matter which window lifecycle hook fires first.
    ///     Deferred to the dispatcher so the bindings of the window are attached before the first update.
    /// </summary>
    public void TryStartLogin(FlexibleWindow window)
    {
        if (!IsDeviceCodeLoginEnabled || _loginStarted) return;

        _loginStarted = true;

        Dispatcher.UIThread.Post(() => _ = RunLoginAsync(window));
    }

    public void Cancel(FlexibleWindow window)
    {
        CancelPendingLogin();
        window.Close();
    }

    /// <summary>
    ///     Called when the login delegate returned false. The dialog stays open so the user can retry.
    /// </summary>
    protected virtual void OnLoginFailed()
    {
    }

    /// <summary>
    ///     Marks the dialog as successfully completed. For logins that bypass the device code flow
    ///     (e.g. a manual token fallback).
    /// </summary>
    protected void MarkSuccess()
    {
        Success = true;
    }

    private async Task RunLoginAsync(FlexibleWindow? window)
    {
        CancelPendingLogin();

        _loginStarted = true;

        var cancellationTokenSource = new CancellationTokenSource();
        _cancellationTokenSource = cancellationTokenSource;
        var token = cancellationTokenSource.Token;

        IsBusy = true;
        UserCode = string.Empty;

        try
        {
            var result = await _loginAction(this, token);

            if (token.IsCancellationRequested) return;

            if (!result)
            {
                OnLoginFailed();
                return;
            }

            Success = true;

            if (window != null) await Dispatcher.UIThread.InvokeAsync(window.Close);
        }
        catch (OperationCanceledException)
        {
            // The user closed the dialog or pressed cancel.
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            Status = e.Message;
            OnLoginFailed();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelPendingLogin()
    {
        var cancellationTokenSource = _cancellationTokenSource;
        _cancellationTokenSource = null;

        if (cancellationTokenSource == null) return;

        try
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed by a previous cancellation.
        }
    }

    private async Task CopyCodeAsync(Control? owner)
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.Clipboard == null) return;

        await topLevel.Clipboard.SetTextAsync(UserCode);
    }

    /// <summary>
    ///     Property setter that can be called from any thread. Login implementations run on background
    ///     threads, and Avalonia bindings require change notifications on the UI thread.
    /// </summary>
    private void SetUiProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;

        field = value;

        OnPropertyChangedFromAnyThread(propertyName);
    }

    private void OnPropertyChangedFromAnyThread(string? propertyName)
    {
        if (Dispatcher.UIThread.CheckAccess()) OnPropertyChanged(propertyName);
        else Dispatcher.UIThread.Post(() => OnPropertyChanged(propertyName));
    }
}
