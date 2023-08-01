using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Dock.Model.Core;

namespace OneWare.Shared.Services;

public interface IProjectExplorerService : IDockable, INotifyPropertyChanged
{
    public ObservableCollection<IProjectEntry> Items { get; }
    public ObservableCollection<IProjectEntry> SelectedItems { get; }
    public IProjectRoot? ActiveProject { get; set; }
    public void Insert(IProjectEntry entry);
    public Task RemoveAsync(params IProjectEntry[] entries);
    public Task DeleteAsync(params IProjectEntry[] entries);
    public IProjectEntry? Search(string path, bool recursive = true);
    public Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager);
    public Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager, params FilePickerFileType[]? filters);
    public IFile GetTemporaryFile(string path);
    public Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName);
    public void ExpandToRoot(IProjectEntry entry);
    public Task ImportFolderDialogAsync(IProjectFolder? destination = null);
    public void ImportFolderRecursive(string source, IProjectFolder destination, params string[] exclude);
    public Task<IProjectEntry> ReloadAsync(IProjectEntry entry);
    public Task SaveLastProjectsFileAsync();
    public Task OpenLastProjectsFileAsync();
}