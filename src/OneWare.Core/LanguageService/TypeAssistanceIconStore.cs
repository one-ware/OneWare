using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Core.LanguageService
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
                { CompletionItemKind.Class, Application.Current.FindResource("Class") as IImage },
                { CompletionItemKind.Constant, Application.Current.FindResource("Constant") as IImage },
                { CompletionItemKind.Constructor, Application.Current.FindResource("Method") as IImage },
                { CompletionItemKind.Enum, Application.Current.FindResource("Enum") as IImage },
                { CompletionItemKind.EnumMember, Application.Current.FindResource("EnumMember") as IImage },
                { CompletionItemKind.Event, Application.Current.FindResource("Event") as IImage },
                { CompletionItemKind.Field, Application.Current.FindResource("Field") as IImage },
                { CompletionItemKind.Function, Application.Current.FindResource("Method") as IImage },
                { CompletionItemKind.Interface, Application.Current.FindResource("Interface") as IImage },
                { CompletionItemKind.Keyword, Application.Current.FindResource("Keyword") as IImage },
                { CompletionItemKind.Method, Application.Current.FindResource("Method") as IImage },
                { CompletionItemKind.Module, Application.Current.FindResource("Module") as IImage },
                { CompletionItemKind.Operator, Application.Current.FindResource("Operator") as IImage },
                { CompletionItemKind.Property, Application.Current.FindResource("Property") as IImage },
                { CompletionItemKind.Snippet, Application.Current.FindResource("Snippet") as IImage },
                { CompletionItemKind.Struct, Application.Current.FindResource("Class") as IImage },
                { CompletionItemKind.Value, Application.Current.FindResource("ValueType") as IImage },
                { CompletionItemKind.Variable, Application.Current.FindResource("Variable") as IImage },
                { CompletionItemKind.Reference, Application.Current.FindResource("Reference") as IImage },
                { CompletionItemKind.TypeParameter, Application.Current.FindResource("TypeParameter") as IImage }
            };

            CustomIcons = new Dictionary<string, IImage?>
            {
                { "Default", Application.Current.FindResource("BoxIcons.RegularCode") as IImage },
                { "Signal", Application.Current.FindResource("PulseGreen") as IImage },
                { "Wait", Application.Current.FindResource("Wait") as IImage },
                { "Package", Application.Current.FindResource("Namespace") as IImage },
                { "ConvertType", Application.Current.FindResource("ConvertType") as IImage },
                { "Component", Application.Current.FindResource("Component") as IImage }
            };
        }
    }
}