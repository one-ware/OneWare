using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public interface IFpgaLoader
{
    public string Id { get; }
    
    public string Name { get; }

    public Task DownloadAsync(UniversalFpgaProjectRoot project);
}