namespace OneWare.PackageManager.ViewModels;

public sealed class PackageSeparatorViewModel(string text) : PackageListEntryViewModel
{
    public string Text { get; } = text;

    public override bool IsSelectable => false;
}
