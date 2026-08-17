namespace OneWare.PackageManager.Models;

/// <summary>
/// Defines how the source string of a configuration profile import is resolved.
/// </summary>
public enum ConfigurationProfileSourceKind
{
    /// <summary>
    /// The source is a local file path and is read directly.
    /// </summary>
    File,

    /// <summary>
    /// The source is an <c>http(s)</c> url that is downloaded first. Falls back to reading a local
    /// file if the string is not an url.
    /// </summary>
    Url
}
