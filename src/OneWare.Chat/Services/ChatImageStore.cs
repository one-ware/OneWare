using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OneWare.Chat.Services;

/// <summary>
/// Stores images that tools returned in a chat, one folder per session, so they survive restarts without bloating
/// the chat history files and are deleted together with their chat.
/// </summary>
internal sealed partial class ChatImageStore(string rootDirectory)
{
    private const string UnassignedFolder = "_unassigned";

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/bmp"] = ".bmp",
        ["image/svg+xml"] = ".svg"
    };

    public string RootDirectory { get; } = rootDirectory;

    public static string GetExtension(string? mimeType) =>
        mimeType != null && Extensions.TryGetValue(mimeType.Split(';')[0].Trim(), out var extension)
            ? extension
            : ".img";

    /// <summary>Writes the image (once per content) and returns its path.</summary>
    public string Store(string? sessionId, byte[] data, string? mimeType)
    {
        var directory = GetSessionDirectory(sessionId);
        Directory.CreateDirectory(directory);

        var hash = Convert.ToHexStringLower(SHA256.HashData(data))[..32];
        var path = Path.Combine(directory, hash + GetExtension(mimeType));
        if (!File.Exists(path))
        {
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, path, true);
        }

        return path;
    }

    public void DeleteSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        var directory = GetSessionDirectory(sessionId);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    public string GetSessionDirectory(string? sessionId) =>
        Path.Combine(RootDirectory, string.IsNullOrWhiteSpace(sessionId)
            ? UnassignedFolder
            : UnsafeCharacters().Replace(sessionId, "_"));

    [GeneratedRegex("[^A-Za-z0-9_.-]|^\\.+$")]
    private static partial Regex UnsafeCharacters();
}
