using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Shared.Converters;
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
        set => SetProperty(ref _offset, value > 0 ? value : 0);
    }

    private long _max;
    public long Max
    {
        get => _max;
        set => SetProperty(ref _max, value);
    }
    
    private double _zoomMultiply = 1;
    public double ZoomMultiply
    {
        get => _zoomMultiply;
        set => SetProperty(ref _zoomMultiply, value);
    }
    
    private long _cursorOffset;
    public long CursorOffset
    {
        get => _cursorOffset;
        set => this.SetProperty(ref _cursorOffset, value >= 0 ? value : 0);
    }
    
    private long _markerOffset = long.MaxValue;
    public long MarkerOffset
    {
        get => _markerOffset;
        set
        {
            this.SetProperty(ref _markerOffset, value >= 0 ? value : 0);
            this.OnPropertyChanged(nameof(MarkerText));
        }
    }
    
    private long _secondMarkerOffset = long.MaxValue;
    
    public long SecondMarkerOffset
    {
        get => _secondMarkerOffset;
        set
        {
            this.SetProperty(ref _secondMarkerOffset, value >= 0 ? value : 0);
            this.OnPropertyChanged(nameof(MarkerText));
        }
    }
    
    public string MarkerText
    {
        get
        {
            if (SecondMarkerOffset == long.MaxValue)
            {
                if (MarkerOffset != long.MaxValue)
                    return TimeUnitConverter.Instance.Convert(MarkerOffset, typeof(string), null, CultureInfo.CurrentCulture)
                        as string ?? "";
                return "0";
            }

            if (MarkerOffset == long.MaxValue) return "0";
            var dist = SecondMarkerOffset - MarkerOffset;
            return (dist > 0 ? "+" : "") + TimeUnitConverter.Instance.Convert(dist, typeof(string), null,
                CultureInfo.CurrentCulture);
        }
    }
    
    public void ZoomIn()
    {
        ZoomMultiply *= 2;
    }

    public void ZoomOut()
    {
        ZoomMultiply /= 2;
    }

    public void ResetView()
    {
        ZoomMultiply = 1;
        Offset = 0;
    }

    public void XOffsetPlus()
    {
        var plus = (long)(Max / ZoomMultiply / 10);
        Offset += plus >= 1 ? plus : 1;
    }

    public void XOffsetMinus()
    {
        var minus = (long)(Max / ZoomMultiply / 10);
        Offset -= minus >= 1 ? minus : 1;
    }

    public void AddSignal(string name, WavePart[] line)
    {
        Signals.Add(new WaveModel(name, WaveColors[Signals.Count % WaveColors.Length], line));
    }
}