namespace OneWare.Essentials.Services;

/// <summary>
/// Keeps secrets such as refresh tokens and API keys out of the settings file: in the operating system's credential
/// store (Keychain, Secret Service) or, on Windows, in files encrypted for the current user.
/// </summary>
/// <remarks>Methods may block on the platform store and throw if it is unavailable; call them off the UI thread.</remarks>
public interface ISecretStore
{
    /// <summary>Returns the secret, or <c>null</c> if none is stored.</summary>
    /// <param name="service">Identifies the owner of the secret, e.g. a host URL or <c>oneware-mcp</c>.</param>
    /// <param name="account">Identifies the secret within the service, e.g. a user id.</param>
    string? Get(string service, string account);

    /// <summary>Stores or replaces the secret.</summary>
    void Set(string service, string account, string secret);

    /// <summary>Deletes the secret if it exists.</summary>
    void Remove(string service, string account);
}
