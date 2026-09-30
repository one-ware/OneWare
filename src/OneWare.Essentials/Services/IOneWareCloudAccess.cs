namespace OneWare.Essentials.Services;

public interface IOneWareCloudAccess
{
    string BaseUrl { get; }

    /// <summary>
    ///     Id of the logged in OneWare Cloud user, or null when logged out.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    ///     Emits the current <see cref="UserId" /> on subscription and whenever the user logs in or out.
    /// </summary>
    IObservable<string?> UserIdObservable { get; }

    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
