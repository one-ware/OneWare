using GitCredentialManager;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Copilot.Services;

/// <summary>
/// Fallback storage for the GitHub token of the Copilot login. Normally the Copilot runtime stores the
/// token in the system keychain itself (<c>account.login</c>); this store is only used when it reports that
/// no secure store was available.
/// </summary>
internal static class CopilotTokenStore
{
    private const string Service = "https://github.com/oneware-copilot";
    private const string Account = "github";

    public static string? Load()
    {
        try
        {
            return CredentialManager.Create("oneware").Get(Service, Account)?.Password;
        }
        catch (Exception e)
        {
            Log(e, "Could not read the stored Copilot GitHub token.");
            return null;
        }
    }

    public static bool Save(string token)
    {
        try
        {
            CredentialManager.Create("oneware").AddOrUpdate(Service, Account, token);
            return true;
        }
        catch (Exception e)
        {
            Log(e, "Could not store the Copilot GitHub token. The login only lasts for this session.");
            return false;
        }
    }

    public static void Clear()
    {
        try
        {
            CredentialManager.Create("oneware").Remove(Service, Account);
        }
        catch (Exception e)
        {
            Log(e, "Could not remove the stored Copilot GitHub token.");
        }
    }

    private static void Log(Exception exception, string message) =>
        ContainerLocator.Container.Resolve<ILogger>().LogWarning(exception, message);
}
