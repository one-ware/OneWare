namespace OneWare.SourceControl.LoginProviders;

public interface ILoginProvider
{
    public string Name { get; }
    public string Host { get; }
    public string GenerateLink { get; }

    public Task<bool> LoginAsync(string password);
}