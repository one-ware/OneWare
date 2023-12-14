using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SDK.NativeTools;

public class NativeTool(string id, string url, string fullPath)
{
    public string Id { get; } = id;
    public string Url { get; } = url;
    public string FullPath { get; } = fullPath;
    
    public bool IsInstalled => Directory.Exists(FullPath);
    public Dictionary<string, NativeToolShortcut> ShortCuts { get; } = new();

    public NativeTool WithShortcut(string shortcutId, string relativePath, string? settingId = null)
    {
        ShortCuts.Add(shortcutId, new NativeToolShortcut(this, relativePath, settingId));
        return this;
    }

    public NativeToolShortcut GetShortcut(string key)
    {
        if (ShortCuts.TryGetValue(key, out var shortCut)) return shortCut;
        throw new Exception("Shortcut not registered");
    }

    public string? GetShorcutPath(string key)
    {
        if (ShortCuts.TryGetValue(key, out var shortCut)) return Path.Combine(FullPath,shortCut.RelativePath);
        return null;
    }
}