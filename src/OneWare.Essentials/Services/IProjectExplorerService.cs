using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using Dock.Model.Core;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IProjectExplorerService : IDockable, INotifyPropertyChanged
{
    /// <summary>
    /// Loaded project roots.
    /// </summary>
    public ObservableCollection<IProjectRoot> Projects { get; }
    /// <summary>
    /// Currently selected explorer nodes.
    /// </summary>
    public IReadOnlyList<IProjectExplorerNode> SelectedItems { get; }
    /// <summary>
    /// Active project root.
    /// </summary>
    public IProjectRoot? ActiveProject { get; set; }
    /// <summary>
    /// Fired when a project is removed.
    /// </summary>
    public event EventHandler<IProjectRoot>? ProjectRemoved;
    /// <summary>
    /// Adds a project root to the explorer.
    /// </summary>
    public void AddProject(IProjectRoot project);
    /// <summary>
    /// Attempts to close a project (with prompts as needed).
    /// </summary>
    public Task TryCloseProjectAsync(IProjectRoot project);
    /// <summary>
    /// Resolves a project root from a file path.
    /// </summary>
    public IProjectRoot? GetRootFromFile(string filePath);
    
    /// <summary>
    /// Shows or constructs the file (if included in the project)
    /// Should not be used except for Show file in Explorer scenarios
    /// </summary>
    public IProjectEntry? GetEntryFromFullPath(string path);
    /// <summary>
    /// Opens a folder picker and loads the chosen project.
    /// </summary>
    public Task<IProjectRoot?> LoadProjectFolderDialogAsync(IProjectManager manager);
    /// <summary>
    /// Opens a file picker and loads the chosen project.
    /// </summary>
    public Task<IProjectRoot?> LoadProjectFileDialogAsync(IProjectManager manager, params FilePickerFileType[]? filters);
    /// <summary>
    /// Loads a project from path and optionally expands/activates it.
    /// </summary>
    public Task<IProjectRoot?> LoadProjectAsync(string path, IProjectManager manager, bool expand = true,
        bool setActive = true);
    /// <summary>
    /// Expands tree nodes up to the root for the given node.
    /// </summary>
    public void ExpandToRoot(IProjectExplorerNode node);
    /// <summary>
    /// Imports files/folders into a project folder.
    /// </summary>
    public Task ImportAsync(IProjectFolder destination, bool copy, bool askForInclude, params string[] paths);
    /// <summary>
    /// Reloads a project from disk.
    /// </summary>
    public Task ReloadProjectAsync(IProjectRoot entry);
    /// <summary>
    /// Saves a project to disk.
    /// </summary>
    public Task SaveProjectAsync(IProjectRoot project);
    /// <summary>
    /// Loads the last opened projects list.
    /// </summary>
    public Task OpenLastProjectsFileAsync();
    /// <summary>
    /// Saves open files that belong to the given project.
    /// </summary>
    public Task<bool> SaveOpenFilesForProjectAsync(IProjectRoot project);
    /// <summary>
    /// Registers a callback to construct context menus.
    /// </summary>
    public void RegisterConstructContextMenu(
        Action<IReadOnlyList<IProjectExplorerNode>, IList<MenuItemModel>> construct);
    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection();
    /// <summary>
    /// Adds a node to the selection.
    /// </summary>
    public void AddToSelection(IProjectExplorerNode node);
    /// <summary>
    /// Returns the list of recent project paths.
    /// </summary>
    public IEnumerable<string> LoadRecentProjects();
}
