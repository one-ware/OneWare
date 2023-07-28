namespace OneWare.Shared;

public interface IWaitForContent
{
    public bool IsContentInitialized { get; }
    public void InitializeContent();
}