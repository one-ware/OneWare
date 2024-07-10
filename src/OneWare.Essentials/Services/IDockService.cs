using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Services;

public interface IDockService : INotifyPropertyChanged
{
    public RootDock? Layout { get; }
    
    public Dictionary<IFile, IExtendedDocument> OpenFiles { get; }
    
    public IExtendedDocument? CurrentDocument { get; }
    
    public void RegisterDocumentView<T>(params string[] extensions) where T : IExtendedDocument;
    
    public void RegisterLayoutExtension<T>(DockShowLocation location);
    
    public void Show<T>(DockShowLocation location = DockShowLocation.Window) where T : IDockable;
    
    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window);
    
    public void CloseDockable(IDockable dockable);
    
    public Task<IExtendedDocument> OpenFileAsync(IFile pf);

    public Task<bool> CloseFileAsync(IFile pf);

    public Window? GetWindowOwner(IDockable dockable);

    public IDockable? SearchView(IDockable instance, IDockable? layout = null);

    public IEnumerable<T> SearchView<T>(IDockable? layout = null);

    public void LoadLayout(string name, bool reset = false);

    public void SaveLayout();

    public void InitializeContent();
}