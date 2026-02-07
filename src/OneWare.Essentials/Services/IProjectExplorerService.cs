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
    public IReadOnlyList<IProjectExplorerNode> SelectedItems { get; }
    public IProjectRoot? ActiveProject { get; set; }
    public event EventHandler<IFile>? FileRemoved;
    public event EventHandler<IProjectRoot>? ProjectRemoved;
    public void Insert(IProjectRoot project);
    public Task RemoveAsync(params IProjectEntry[] entries);
    public Task DeleteAsync(params IProjectEntry[] entries);
    public IProjectEntry? GetEntry(string relativePath);
    public IProjectEntry? GetEntryFromFullPath(string path);
    public Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager);

    public Task<IProjectRoot?>
        LoadProjectFileDialogAsync(IProjectManager manager, params FilePickerFileType[]? filters);

    public Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true,
        bool setActive = true);

    public IFile GetTemporaryFile(string path);
    public void RemoveTemporaryFile(IFile file);
    public Task<IProjectEntry> RenameAsync(IProjectEntry entry, string newName);
    public void ExpandToRoot(IProjectExplorerNode node);
    public Task ImportFolderDialogAsync(IProjectFolder? destination = null);
    public Task ImportAsync(IProjectFolder destination, bool copy, bool askForInclude, params string[] paths);
    public Task<IProjectEntry> ReloadAsync(IProjectEntry entry);
    public Task SaveProjectAsync(IProjectRoot project);
    public Task SaveRecentProjectsFileAsync();
    public Task SaveLastProjectsFileAsync();
    public Task OpenLastProjectsFileAsync();
    public Task<bool> SaveOpenFilesForProjectAsync(IProjectRoot project);

    public void RegisterConstructContextMenu(
        Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemViewModel>> construct);

    public void ClearSelection();
    public void AddToSelection(IProjectExplorerNode node);
    public IEnumerable<string> LoadRecentProjects();
}