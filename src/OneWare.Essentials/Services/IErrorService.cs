using Dock.Model.Core;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Services;

public interface IErrorService : IDockable
{
    public void RegisterErrorSource(string source);
    public event EventHandler<object?>? ErrorRefresh;
    public void Clear(string source);
    public void ClearFile(string filePath);
    public void RefreshErrors(IList<ErrorListItem> errors, string source, string filePath);
    public IEnumerable<ErrorListItem> GetErrors();
    public IEnumerable<ErrorListItem> GetErrorsForFile(string filePath);
}
