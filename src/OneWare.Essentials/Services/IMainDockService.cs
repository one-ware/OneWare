using System.ComponentModel;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IMainDockService : INotifyPropertyChanged
{
    /// <summary>
    /// Current dock layout root.
    /// </summary>
    public RootDock? Layout { get; }

    /// <summary>
    /// Map of open file paths to document view models.
    /// </summary>
    public Dictionary<string, IExtendedDocument> OpenFiles { get; }

    /// <summary>
    /// Currently active document.
    /// </summary>
    public IExtendedDocument? CurrentDocument { get; }

    /// <summary>
    /// Registers a document view model for the given extensions.
    /// </summary>
    public void RegisterDocumentView<T>(params string[] extensions) where T : IExtendedDocument;

    /// <summary>
    /// Registers an override handler for opening files with the given extensions.
    /// </summary>
    public void RegisterFileOpenOverwrite(Func<string, bool> action, params string[] extensions);

    /// <summary>
    /// Registers a dockable layout extension shown at the specified location.
    /// </summary>
    public void RegisterLayoutExtension<T>(DockShowLocation location);

    /// <summary>
    /// Shows a dockable of the specified type.
    /// </summary>
    public void Show<T>(DockShowLocation location = DockShowLocation.Window) where T : IDockable;

    /// <summary>
    /// Shows the given dockable.
    /// </summary>
    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window);

    /// <summary>
    /// Closes the given dockable.
    /// </summary>
    public void CloseDockable(IDockable dockable);

    /// <summary>
    /// Opens a file into a document view.
    /// </summary>
    public Task<IExtendedDocument?> OpenFileAsync(string fullPath);

    /// <summary>
    /// Closes a file if it is open.
    /// </summary>
    public Task<bool> CloseFileAsync(string fullPath);

    /// <summary>
    /// Removes a file from the open file registry.
    /// </summary>
    public void UnregisterOpenFile(string fullPath);

    /// <summary>
    /// Returns the window that owns the given dockable.
    /// </summary>
    public Window? GetWindowOwner(IDockable dockable);

    /// <summary>
    /// Searches for a dockable instance in the layout.
    /// </summary>
    public IDockable? SearchView(IDockable instance, IDockable? layout = null);

    /// <summary>
    /// Searches for dockables of the given type in the layout.
    /// </summary>
    public IEnumerable<T> SearchView<T>(IDockable? layout = null);

    /// <summary>
    /// Loads a named layout, optionally resetting first.
    /// </summary>
    public void LoadLayout(string name, bool reset = false);

    /// <summary>
    /// Saves the current layout.
    /// </summary>
    public void SaveLayout();

    /// <summary>
    /// Initializes content for docked views when required.
    /// </summary>
    public void InitializeContent();
}
