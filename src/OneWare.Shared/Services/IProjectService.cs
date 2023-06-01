using System.Collections.ObjectModel;
using System.ComponentModel;
using Dock.Model.Core;

namespace OneWare.Shared.Services;

public interface IProjectService : IDockable, INotifyPropertyChanged
{
    public ObservableCollection<IProjectEntry> Items { get; }
    public ObservableCollection<IProjectEntry> SelectedItems { get; }
    public IProjectRoot? ActiveProject { get; set; }
    public Task DeleteAsync(params IProjectEntry[] entries);
    public IProjectEntry? Search(string path, bool recursive = true);
    public Task<IProjectRoot?> LoadProjectAsync(string path);
    public IFile GetTemporaryFile(string path);
    public Task HandleFileChangeAsync(string path);
    public Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName);
    public void ExpandToRoot(IProjectEntry entry);
}