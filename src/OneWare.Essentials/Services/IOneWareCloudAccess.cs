namespace OneWare.Essentials.Services;

public interface IOneWareCloudAccess
{
    string BaseUrl { get; }

    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
