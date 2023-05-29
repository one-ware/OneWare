namespace OneWare.Shared.Services;

public interface ILanguageManager
{
    public void RegisterService<T>(bool workspaceDependent);
}