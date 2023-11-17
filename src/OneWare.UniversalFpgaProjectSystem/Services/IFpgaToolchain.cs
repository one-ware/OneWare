namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaToolchain
{
    public string Name { get; }

    public void LoadConnections();

    public void SaveConnections();

    public void StartCompile();
}