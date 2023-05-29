using OneWare.Shared.LanguageService;

namespace OneWare.Shared.Services;

public interface ILanguageManager
{
    public void RegisterService(Type type, bool workspaceDependent);
}