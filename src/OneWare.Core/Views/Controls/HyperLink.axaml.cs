using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Prism.Ioc;
using OneWare.Shared;
using OneWare.Shared.Services;

namespace OneWare.Core.Views.Controls
{
    public partial class HyperLink : UserControl
    {
        public static readonly StyledProperty<string> UrlProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Url));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Label));

        public static readonly StyledProperty<TextDecorationCollection> TextDecorationsProperty =
            AvaloniaProperty.Register<TextBlock, TextDecorationCollection>(nameof(TextDecorations));

        public HyperLink()
        {
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

        public void Open_Click(object? sender, RoutedEventArgs e)
        {
            if (File.Exists(Url))
            {
                var file = ContainerLocator.Container.Resolve<IProjectService>().GetTemporaryFile(Url);
                _ = ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(file);
            }
            else
            {
                Tools.OpenHyperLink(Url);
            }
        }
    }
}