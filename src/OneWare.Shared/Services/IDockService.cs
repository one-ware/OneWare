using System.ComponentModel;
using Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Services;

public interface IDockService : INotifyPropertyChanged
{
    public RootDock? Layout { get; }
    
    public IExtendedDocument? CurrentDocument { get; }

    public void RegisterLayoutExtension<T>(DockShowLocation location);
    
    public Dictionary<IFile, IExtendedDocument> OpenFiles { get; }
    
    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window);
    
    public Task<IExtendedDocument> OpenFileAsync(IFile pf);

    public Task<bool> CloseFileAsync(IFile pf);

    public Window? GetWindowOwner(IDockable dockable);
    
    public void LoadLayout(string name, bool reset = false);

    public void SaveLayout();

    public void InitializeDocuments();
}