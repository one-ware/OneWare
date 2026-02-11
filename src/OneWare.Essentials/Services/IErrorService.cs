using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IErrorService : IDockable
{
    /// <summary>
    /// Registers a named error source.
    /// </summary>
    public void RegisterErrorSource(string source);
    /// <summary>
    /// Fired when error data is refreshed.
    /// </summary>
    public event EventHandler<object?>? ErrorRefresh;
    /// <summary>
    /// Clears all errors for a source.
    /// </summary>
    public void Clear(string source);
    /// <summary>
    /// Clears errors for a specific file.
    /// </summary>
    public void ClearFile(string filePath);
    /// <summary>
    /// Replaces errors for a file and source.
    /// </summary>
    public void RefreshErrors(IList<ErrorListItem> errors, string source, string filePath);
    /// <summary>
    /// Returns all errors.
    /// </summary>
    public IEnumerable<ErrorListItem> GetErrors();
    /// <summary>
    /// Returns errors for a specific file.
    /// </summary>
    public IEnumerable<ErrorListItem> GetErrorsForFile(string filePath);
}
