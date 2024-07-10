using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.LanguageService
{
    public class TypeAssistanceIconStore
    {
        public static readonly TypeAssistanceIconStore Instance = new();

        private Dictionary<CompletionItemKind, IImage?>? _icons;
        
        public Dictionary<CompletionItemKind, IImage?> Icons
        {
            get
            {
                if (_icons != null) return _icons;
                throw new NullReferenceException("TypeAssistanceIconStore not loaded yet!");
            }
            private set => _icons = value;
        }

        private Dictionary<string, IImage?>? _customIcons;
        public Dictionary<string, IImage?> CustomIcons
        {
            get
            {
                if (_customIcons != null) return _customIcons;
                throw new NullReferenceException("TypeAssistanceIconStore not loaded yet!");
            }
            private set => _customIcons = value;
        }

        public void Load()
        {
            if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));
            
            Icons = new Dictionary<CompletionItemKind, IImage?>
            {
                { CompletionItemKind.Class, Application.Current.FindResource(Application.Current.RequestedThemeVariant, "Class") as IImage },
                { CompletionItemKind.Constant, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Constant") as IImage },
                { CompletionItemKind.Constructor, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Method") as IImage },
                { CompletionItemKind.Enum, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Enum") as IImage },
                { CompletionItemKind.EnumMember, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"EnumMember") as IImage },
                { CompletionItemKind.Event, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Event") as IImage },
                { CompletionItemKind.Field, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Field") as IImage },
                { CompletionItemKind.Function, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Method") as IImage },
                { CompletionItemKind.Interface, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Interface") as IImage },
                { CompletionItemKind.Keyword, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Keyword") as IImage },
                { CompletionItemKind.Method, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Method") as IImage },
                { CompletionItemKind.Module, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Module") as IImage },
                { CompletionItemKind.Operator, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Operator") as IImage },
                { CompletionItemKind.Property, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Property") as IImage },
                { CompletionItemKind.Snippet, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Snippet") as IImage },
                { CompletionItemKind.Struct, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Class") as IImage },
                { CompletionItemKind.Value, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"ValueType") as IImage },
                { CompletionItemKind.Variable, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Variable") as IImage },
                { CompletionItemKind.Reference, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Reference") as IImage },
                { CompletionItemKind.TypeParameter, Application.Current.FindResource(Application.Current.RequestedThemeVariant,"TypeParameter") as IImage }
            };

            CustomIcons = new Dictionary<string, IImage?>
            {
                { "Default", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"BoxIcons.RegularCode") as IImage },
                { "Signal", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"PulseGreen") as IImage },
                { "Wait", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Wait") as IImage },
                { "Package", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Namespace") as IImage },
                { "ConvertType", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"ConvertType") as IImage },
                { "Component", Application.Current.FindResource(Application.Current.RequestedThemeVariant,"Component") as IImage }
            };
        }
    }
}