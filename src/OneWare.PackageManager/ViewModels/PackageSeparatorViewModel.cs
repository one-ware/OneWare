namespace OneWare.PackageManager.ViewModels;

public sealed class PackageSeparatorViewModel(string text, bool showLine) : PackageListEntryViewModel
{
    public string Text { get; } = text;

    public bool ShowLine { get; } = showLine;

    public override bool IsSelectable => false;
}
