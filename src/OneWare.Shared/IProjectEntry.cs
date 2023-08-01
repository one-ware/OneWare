using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;

namespace OneWare.Shared;

public interface IProjectEntry : IHasPath
{
    public new string Header { get; set; }

    public ObservableCollection<IProjectEntry> Items { get; }
    
    public string RelativePath { get; }
    
    public IImage Icon { get; }
    
    public bool IsExpanded { get; set; }
    
    public IBrush Background { get; set; }
    
    public FontWeight FontWeight { get; set; }
    
    public float TextOpacity { get; }

    public IProjectRoot Root { get; }
    
    public IProjectFolder? TopFolder { get; set; }

    public Action<Action<string>>? RequestRename { get; set; }
    
    public bool IsValid();
}