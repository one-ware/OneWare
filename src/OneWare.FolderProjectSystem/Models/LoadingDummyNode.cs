using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.FolderProjectSystem.Models;

public class LoadingDummyNode : IProjectExplorerNode
{
    public IImage? Icon { get; } = null;
    
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Header => "Loading...";
    public IProjectExplorerNode? Parent { get; } = null;
    public ObservableCollection<IProjectExplorerNode> Children { get; } = new();
    public ObservableCollection<IImage> IconOverlays { get; } = new();
    public bool IsExpanded { get; set; }
    public IBrush Background { get; set; } = Brushes.Transparent;
    public FontWeight FontWeight { get; set; }
    public float TextOpacity { get; set; } = 1f;
}