using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SDK.NativeTools;

public class NativeTool(string url, string fullPath)
{
    public string Url { get; } = url;
    public string FullPath { get; } = fullPath;
    
    public bool IsInstalled => Directory.Exists(FullPath);
    public Dictionary<string, NativeToolShortcut> ShortCuts { get; } = new();

    public NativeTool WithShortcut(string shortcutId, string relativePath, string? settingId = null)
    {
        ShortCuts.Add(shortcutId, new NativeToolShortcut(relativePath, settingId));
        return this;
    }

    public NativeToolShortcut GetShortcut(string key)
    {
        if (ShortCuts.TryGetValue(key, out var shortCut)) return shortCut;
        throw new Exception("Shortcut not registered");
    }

    public string? GetShortcutPath(string key)
    {
        if (ShortCuts.TryGetValue(key, out var shortCut)) return Path.Combine(FullPath,shortCut.RelativePath);
        return null;
    }
}