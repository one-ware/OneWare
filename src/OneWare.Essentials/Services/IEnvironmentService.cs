namespace OneWare.Essentials.Services;

public interface IEnvironmentService
{
    /// <summary>
    /// Sets an environment variable for the current process.
    /// </summary>
    public void SetEnvironmentVariable(string key, string? value);

    /// <summary>
    /// Updates a PATH-like environment variable.
    /// </summary>
    public void SetPath(string key, string? path);
}
