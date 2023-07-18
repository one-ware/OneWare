using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.WaveFormViewer.Models;

namespace OneWare.WaveFormViewer.ViewModels;

public class WaveFormViewModel : ObservableObject
{
    private static readonly IBrush[] WaveColors =
        { Brushes.Lime, Brushes.Magenta, Brushes.Yellow, Brushes.CornflowerBlue };
    
    public ObservableCollection<WaveModel> Signals { get; } = new();
    
    private long _offset;
    public long Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value);
    }
}