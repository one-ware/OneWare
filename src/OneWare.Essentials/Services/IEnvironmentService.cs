namespace OneWare.Essentials.Services;

public interface IEnvironmentService
{
    public void SetEnvironmentVariable(string key, string? value);
    
    public void SetPath(string key, string? path);
}