namespace OneWare.PackageManager.ViewModels;

public sealed class PackageSeparatorViewModel(string text, bool showLine)
{
    public string Text { get; } = text;

    public bool ShowLine { get; } = showLine;
}
