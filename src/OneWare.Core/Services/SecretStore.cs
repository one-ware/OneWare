using System.Security.Cryptography;
using System.Text;
using GitCredentialManager;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

/// <summary>
/// Windows stores secrets in DPAPI-encrypted files because Credential Manager entries are limited to 2.5 KB, which
/// long tokens exceed. Other platforms use the system credential store through Git Credential Manager.
/// </summary>
public sealed class SecretStore(IPaths paths) : ISecretStore
{
    private const string Namespace = "oneware";

    public string? Get(string service, string account)
    {
        if (!OperatingSystem.IsWindows()) return CredentialManager.Create(Namespace).Get(service, account)?.Password;

        var path = GetWindowsPath(service, account);
        if (!File.Exists(path)) return null;
        var plaintext = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plaintext);
    }

    public void Set(string service, string account, string secret)
    {
        if (!OperatingSystem.IsWindows())
        {
            CredentialManager.Create(Namespace).AddOrUpdate(service, account, secret);
            return;
        }

        var path = GetWindowsPath(service, account);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path,
            ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.CurrentUser));
    }

    public void Remove(string service, string account)
    {
        if (!OperatingSystem.IsWindows())
        {
            CredentialManager.Create(Namespace).Remove(service, account);
            return;
        }

        var path = GetWindowsPath(service, account);
        if (File.Exists(path)) File.Delete(path);
    }

    // Hashed, because services and accounts may be URLs or contain characters that are invalid in file names.
    private string GetWindowsPath(string service, string account)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{service}\n{account}"));
        return Path.Combine(paths.AppDataDirectory, "Secrets", Convert.ToHexString(hash) + ".bin");
    }
}
