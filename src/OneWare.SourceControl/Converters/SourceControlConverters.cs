using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OneWare.SourceControl.Models;

namespace OneWare.SourceControl.Converters;

public static class SourceControlConverters
{
    public static readonly IValueConverter ChangeStatusBrushConverter = new ChangeStatusBrushConverter();
    public static readonly IValueConverter ChangeStatusCharConverter = new ChangeStatusCharConverter();

    public static readonly IValueConverter IsTagConverter =
        new FuncValueConverter<GitRefKind, bool>(kind => kind == GitRefKind.Tag);

    public static readonly IValueConverter RefKindIconConverter = new FuncValueConverter<GitRefKind, Geometry?>(kind =>
        Application.Current?.FindResource(kind switch
        {
            GitRefKind.Head => "Icon.GitCommit",
            GitRefKind.RemoteBranch => "Icon.Cloud",
            GitRefKind.Tag => "Icon.Tag",
            _ => "Icon.GitBranch"
        }) as Geometry);
}
