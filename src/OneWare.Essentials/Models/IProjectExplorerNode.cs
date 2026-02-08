using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;

namespace OneWare.Essentials.Models;

/// <summary>
///     Node displayed in the project explorer
/// </summary>
public interface IProjectExplorerNode : INotifyPropertyChanged
{
    public IconModel? Icon { get; }
    
    public string Header { get; }

    public IProjectExplorerNode? Parent { get; }

    public ObservableCollection<IProjectExplorerNode>? Children { get; }

    public bool IsExpanded { get; set; }

    public IBrush Background { get; set; }

    public FontWeight FontWeight { get; set; }

    public float TextOpacity { get; set; }
}