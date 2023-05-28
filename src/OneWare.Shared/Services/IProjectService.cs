using System.Collections.ObjectModel;
using Dock.Model.Core;

namespace OneWare.Shared.Services;

public interface IProjectService : IDockable
{
    public ObservableCollection<IProjectEntry> Items { get; }
    public IProjectRoot? ActiveProject { get; set; }
    public IProjectEntry? Search(string path, bool recursive = true);
    public Task<IProjectRoot?> LoadProjectAsync(string path);
    public IFile GetTemporaryFile(string path);
    public Task HandleFileChangeAsync(string path);
    public Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName);
}