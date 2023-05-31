using Dock.Model.Core;
using OneWare.Shared.Models;

namespace OneWare.Shared.Services;

public interface IErrorService : IDockable
{
    public event EventHandler<object?>? ErrorRefresh;
    public void Clear(string source);
    public void RefreshErrors(IList<ErrorListItemModel> errors, string source, IFile entry);
    public IEnumerable<ErrorListItemModel> GetErrorsForFile(IFile file);
}