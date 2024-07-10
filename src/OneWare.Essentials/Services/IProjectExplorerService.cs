using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Dock.Model.Core;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IProjectExplorerService : IDockable, INotifyPropertyChanged
{
    public ObservableCollection<IProjectRoot> Projects { get; }
    public ObservableCollection<IProjectExplorerNode> SelectedItems { get; }
    public IProjectRoot? ActiveProject { get; set; }
    public event EventHandler<IFile>? FileRemoved;
    public event EventHandler<IProjectRoot>? ProjectRemoved;
    public void Insert(IProjectRoot project);
    public Task RemoveAsync(params IProjectEntry[] entries);
    public Task DeleteAsync(params IProjectEntry[] entries);
    public IProjectEntry? SearchName(string path, bool recursive = true);
    public IProjectEntry? SearchFullPath(string path, bool recursive = true);
    public Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager);
    public Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager, params FilePickerFileType[]? filters);
    public IFile GetTemporaryFile(string path);
    public void RemoveTemporaryFile(IFile file);
    public Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName);
    public void ExpandToRoot(IProjectExplorerNode node);
    public Task ImportFolderDialogAsync(IProjectFolder? destination = null);
    public Task<IProjectEntry> ReloadAsync(IProjectEntry entry);
    public Task SaveProjectAsync(IProjectRoot project);
    public Task SaveLastProjectsFileAsync();
    public Task OpenLastProjectsFileAsync();
    public void RegisterConstructContextMenu(Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemViewModel>> construct);
}