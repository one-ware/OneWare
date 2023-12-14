using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SDK.NativeTools;

public class NativeToolShortcut(NativeTool owner, string relativePath, string? settingKey = null) : ObservableObject
{
    public NativeTool Owner { get; } = owner;
    public string RelativePath { get; } = relativePath;
    public string? SettingKey { get; } = settingKey;
}