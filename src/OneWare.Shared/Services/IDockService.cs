using System.ComponentModel;
using Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Services;

public interface IDockService : INotifyPropertyChanged
{
    public IRootDock? Layout { get; }
    
    public IExtendedDocument? CurrentDocument { get; set; }
    
    public void RegisterLayoutExtension<T>(DockShowLocation location);
    
    public Dictionary<IFile, IExtendedDocument> OpenFiles { get; }
    
    public void Show(IDockable dockable, DockShowLocation location = DockShowLocation.Window);
    
    public Task<IDockable> OpenFileAsync(IFile pf);

    public Task<bool> CloseFileAsync(IFile pf);

    public Window GetWindowOwner(IDockable dockable);
    
    public void LoadLayout(string name, bool reset = false);

    public void SaveLayout();
}