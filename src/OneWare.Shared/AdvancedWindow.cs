using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace OneWare.Shared
{
    public class AdvancedWindow : Window, IStyleable
    {
        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<AdvancedWindow, bool>(nameof(ShowTitleProperty), true);
        
        public static readonly StyledProperty<IImage?> CustomIconProperty =
            AvaloniaProperty.Register<AdvancedWindow, IImage?>(nameof(CustomIcon));

        public static readonly StyledProperty<Control?> TitleBarContentProperty =
            AvaloniaProperty.Register<AdvancedWindow, Control?>(nameof(TitleBarContent));
        
        public static readonly StyledProperty<Control?> BottomContentProperty =
            AvaloniaProperty.Register<AdvancedWindow, Control?>(nameof(BottomContent));
        
        public static readonly StyledProperty<HorizontalAlignment> HorizontalAlignmentTitleProperty =
            AvaloniaProperty.Register<AdvancedWindow, HorizontalAlignment>(nameof(HorizontalAlignmentTitle), HorizontalAlignment.Left);
        
        Type IStyleable.StyleKey => typeof(AdvancedWindow);

        public bool ShowTitle
        {
            get => GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        public IImage? CustomIcon
        {
            get => GetValue(CustomIconProperty);
            set => SetValue(CustomIconProperty, value);
        }

        public Control? TitleBarContent
        {
            get => GetValue(TitleBarContentProperty);
            set => SetValue(TitleBarContentProperty, value);
        }
        
        public Control? BottomContent
        {
            get => GetValue(BottomContentProperty);
            set => SetValue(BottomContentProperty, value);
        }

        public HorizontalAlignment HorizontalAlignmentTitle
        {
            get => GetValue(HorizontalAlignmentTitleProperty);
            set => SetValue(HorizontalAlignmentTitleProperty, value);
        }

        public AdvancedWindow()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                HorizontalAlignmentTitle = HorizontalAlignment.Center;
            }
        }
        
        protected override void HandleWindowStateChanged(WindowState state)
        {
            base.HandleWindowStateChanged(state);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ExtendClientAreaTitleBarHeightHint = state is WindowState.Maximized or WindowState.FullScreen ? 37 : 30;
            }
        }
    }
}