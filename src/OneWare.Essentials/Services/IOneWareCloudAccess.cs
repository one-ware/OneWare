namespace OneWare.Essentials.Services;

public sealed record OneWareCloudOrganization(Guid Id, string Name);

public interface IOneWareCloudAccess
{
    string BaseUrl { get; }

    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    Task<Guid?> GetDefaultOrganizationIdAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OneWareCloudOrganization>> GetOrganizationsAsync(
        CancellationToken cancellationToken = default);
}
