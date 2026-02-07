using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;

namespace OneWare.Essentials.Models;

public class LoadingDummyNode : IProjectExplorerNode
{
    public IImage? Icon { get; } = null;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Header => "Loading...";
    public IProjectExplorerNode? Parent { get; } = null;
    public ObservableCollection<IProjectExplorerNode> Children { get; } = new();
    public ObservableCollection<IImage> IconOverlays { get; } = new();
    public ObservableCollection<IImage> RightIcons { get; } = new();

    public bool IsExpanded
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }

    public IBrush Background
    {
        get;
        set
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(nameof(Background));
        }
    } = Brushes.Transparent;

    public FontWeight FontWeight
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            OnPropertyChanged(nameof(FontWeight));
        }
    } = FontWeight.Regular;

    public float TextOpacity
    {
        get;
        set
        {
            if (Math.Abs(field - value) < float.Epsilon) return;
            field = value;
            OnPropertyChanged(nameof(TextOpacity));
        }
    } = 1f;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}