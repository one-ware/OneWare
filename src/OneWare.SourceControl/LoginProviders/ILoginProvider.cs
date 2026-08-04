namespace OneWare.SourceControl.LoginProviders;

public interface ILoginProvider
{
    public string Name { get; }
    public string Host { get; }
    public string GenerateLink { get; }

    public Task<bool> LoginAsync(string password);
}

/// <summary>
///     Implemented by login providers that support the OAuth 2.0 Device Authorization Grant (RFC 8628).
/// </summary>
public interface IDeviceCodeLoginProvider : ILoginProvider
{
    /// <summary>
    ///     False if the provider is not configured for OAuth (e.g. missing client id).
    ///     In that case the UI falls back to manual token entry.
    /// </summary>
    public bool IsDeviceCodeLoginAvailable { get; }

    public Task<DeviceCodeStartResult> StartDeviceCodeLoginAsync(CancellationToken cancellationToken = default);

    public Task<DeviceCodeLoginResult> CompleteDeviceCodeLoginAsync(DeviceCodeInfo deviceCode,
        CancellationToken cancellationToken = default);
}

public record DeviceCodeInfo(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    TimeSpan Interval,
    DateTimeOffset ExpiresAt);

public record DeviceCodeStartResult(DeviceCodeInfo? DeviceCode, string? ErrorMessage)
{
    public static DeviceCodeStartResult Ok(DeviceCodeInfo deviceCode)
    {
        return new DeviceCodeStartResult(deviceCode, null);
    }

    public static DeviceCodeStartResult Error(string message)
    {
        return new DeviceCodeStartResult(null, message);
    }
}

public enum DeviceCodeLoginStatus
{
    Success,
    Expired,
    AccessDenied,
    Cancelled,
    Error
}

public record DeviceCodeLoginResult(DeviceCodeLoginStatus Status, string? Username, string? ErrorMessage)
{
    public bool IsSuccess => Status == DeviceCodeLoginStatus.Success;

    public static DeviceCodeLoginResult Ok(string username)
    {
        return new DeviceCodeLoginResult(DeviceCodeLoginStatus.Success, username, null);
    }

    public static DeviceCodeLoginResult Failed(DeviceCodeLoginStatus status, string message)
    {
        return new DeviceCodeLoginResult(status, null, message);
    }
}
