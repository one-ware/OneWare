using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.FolderProjectSystem.Models;

public class LoadingDummyNode : IProjectExplorerNode
{
    private IBrush _background = Brushes.Transparent;
    private FontWeight _fontWeight = FontWeight.Regular;
    private bool _isExpanded;
    private float _textOpacity = 1f;

    public IImage? Icon { get; } = null;

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Header => "Loading...";
    public IProjectExplorerNode? Parent { get; } = null;
    public ObservableCollection<IProjectExplorerNode> Children { get; } = new();
    public ObservableCollection<IImage> IconOverlays { get; } = new();
    public ObservableCollection<IImage> RightIcons { get; } = new();
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged(nameof(IsExpanded));
        }
    }
    public IBrush Background
    {
        get => _background;
        set
        {
            if (Equals(_background, value)) return;
            _background = value;
            OnPropertyChanged(nameof(Background));
        }
    }
    public FontWeight FontWeight
    {
        get => _fontWeight;
        set
        {
            if (_fontWeight == value) return;
            _fontWeight = value;
            OnPropertyChanged(nameof(FontWeight));
        }
    }
    public float TextOpacity
    {
        get => _textOpacity;
        set
        {
            if (Math.Abs(_textOpacity - value) < float.Epsilon) return;
            _textOpacity = value;
            OnPropertyChanged(nameof(TextOpacity));
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
