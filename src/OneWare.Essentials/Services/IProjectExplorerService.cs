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
    public event EventHandler<IProjectRoot>? ProjectRemoved;
    public void AddProject(IProjectRoot project);
    public Task TryCloseProjectAsync(IProjectRoot project);
    public IProjectRoot? GetRootFromFile(string filePath);
    
    /// <summary>
    /// Shows or constructs the file (if included in the project)
    /// Should not be used except for Show file in Explorer scenarios
    /// </summary>
    public IProjectEntry? GetEntryFromFullPath(string path);
    public Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager);
    public Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager, params FilePickerFileType[]? filters);
    public Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true,
        bool setActive = true);
    public void ExpandToRoot(IProjectExplorerNode node);
    public Task ImportAsync(IProjectFolder destination, bool copy, bool askForInclude, params string[] paths);
    public Task ReloadProjectAsync(IProjectRoot entry);
    public Task SaveProjectAsync(IProjectRoot project);
    public Task OpenLastProjectsFileAsync();
    public Task<bool> SaveOpenFilesForProjectAsync(IProjectRoot project);
    public void RegisterConstructContextMenu(
        Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemViewModel>> construct);
    public void ClearSelection();
    public void AddToSelection(IProjectExplorerNode node);
    public IEnumerable<string> LoadRecentProjects();
}
