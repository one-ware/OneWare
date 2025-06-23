using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Toml;

public class TypeAssistanceToml : TypeAssistanceBase
{
    public TypeAssistanceToml(IEditor editor, ISettingsService settingsService) : base(editor, settingsService)
    {
        LineCommentSequence = "#";
    }
}