using OneWare.SDK.Helpers;

namespace OneWare.SDK.NativeTools;

public class NativeToolContainer(string id, string fullPath)
{
    public string Id { get; } = id;
    public string FullPath { get; } = fullPath;
    public Dictionary<PlatformId, NativeTool> Platforms { get; } = new();

    public NativeTool AddPlatform(PlatformId platform, string url)
    {
        Platforms[platform] = new NativeTool(url, Path.Combine(FullPath, platform.ToString()));
        return Platforms[platform];
    }
    
    public NativeTool? GetPlatform(PlatformId? platform = null)
    {
        var plt = platform ?? PlatformHelper.Platform;

        //If OsxArm64 is not available, use X64 to be used with Rosetta
        if (plt == PlatformId.OsxArm64 && !Platforms.ContainsKey(PlatformId.OsxArm64) &&
            Platforms.ContainsKey(PlatformId.OsxX64))
        {
            plt = PlatformId.OsxX64;
        }
        return Platforms.GetValueOrDefault(plt);
    }

    public string GetShortcutPathOrEmpty(string shortcut, PlatformId? platform = null)
    {
        return GetPlatform(platform)?.GetShortcutPath(shortcut) ?? string.Empty;
    }
}