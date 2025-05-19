using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Controls;

/// <summary>
/// A clickable hyperlink control that either opens a file or launches a URL in the default browser.
/// </summary>
public partial class HyperLink : UserControl
{
    public static readonly StyledProperty<string> UrlProperty =
        AvaloniaProperty.Register<HyperLink, string>(nameof(Url));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<HyperLink, string>(nameof(Label));

    public static readonly StyledProperty<TextDecorationCollection> TextDecorationsProperty =
        AvaloniaProperty.Register<HyperLink, TextDecorationCollection>(
            nameof(TextDecorations),
            new TextDecorationCollection());

    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IDockService _dockService;
    private readonly PlatformHelper _platformHelper;

    public HyperLink(
        IProjectExplorerService projectExplorerService,
        IDockService dockService,
        PlatformHelper platformHelper)
    {
        _projectExplorerService = projectExplorerService;
        _dockService = dockService;
        _platformHelper = platformHelper;

        InitializeComponent();
    }

    public string Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public TextDecorationCollection TextDecorations
    {
        get => GetValue(TextDecorationsProperty);
        set => SetValue(TextDecorationsProperty, value);
    }

    public async void Open_Click(object? sender, RoutedEventArgs e)
    {
        if (File.Exists(Url))
        {
            var file = _projectExplorerService.GetTemporaryFile(Url);
            await _dockService.OpenFileAsync(file);
        }
        else
        {
            _platformHelper.OpenHyperLink(Url);
        }
    }
}
