using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using OneWare.Essentials.Helpers;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.Models;

namespace OneWare.WaveFormViewer.ViewModels;

public class WaveFormViewModel : ObservableObject
{
    public int MinZoomLevel { get; } = -2;
    public int MaxZoomLevel { get; } = 20;

    private static readonly IBrush[] WaveColors =
        [Brushes.Lime, Brushes.Cyan, Brushes.Magenta, Brushes.Yellow];

    public ObservableCollection<WaveModel> Signals { get; } = [];

    private long _timeScale = 1;

    /// <summary>
    /// 1 = 1 fs
    /// </summary>
    public long TimeScale
    {
        get => _timeScale;
        set => SetProperty(ref _timeScale, value);
    }

    private bool _extendSignals;

    public bool ExtendSignals
    {
        get => _extendSignals;
        set => SetProperty(ref _extendSignals, value);
    }

    private long _offset;

    public long Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value > 0 ? value : 0);
    }

    public long ViewPortWidth => (long)(Max / ZoomMultiply);

    private long _max;

    public long Max
    {
        get => _max;
        set
        {
            SetProperty(ref _max, value);
            OnPropertyChanged(nameof(ViewPortWidth));
            OnPropertyChanged(nameof(MaxScroll));
        }
    }
    
    public long MaxScroll => Max - ViewPortWidth;

    private int _zoomLevel;

    public int ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (value < MinZoomLevel) value = MinZoomLevel;
            if (value > MaxZoomLevel) value = MaxZoomLevel;
            
            SetProperty(ref _zoomLevel, value);

            ZoomMultiply = Math.Pow(2, value);
            
            if (ZoomMultiply >= 1)
            {
                var currentOffsetWidth = Max / (long)ZoomMultiply;
                if(MarkerOffset != long.MaxValue)
                    Offset = MarkerOffset - currentOffsetWidth / 2;
            }
        }
    }
    private double _zoomMultiply = 1;

    public double ZoomMultiply
    {
        get => _zoomMultiply;
        private set
        {
            SetProperty(ref _zoomMultiply, value);
            OnPropertyChanged(nameof(ViewPortWidth));
            OnPropertyChanged(nameof(MaxScroll));
        }
    }

    private long _cursorOffset;

    public long CursorOffset
    {
        get => _cursorOffset;
        set
        {
            SetProperty(ref _cursorOffset, value >= 0 ? value : 0);
            this.OnPropertyChanged(nameof(CursorText));
        }
    }

    private long _markerOffset = long.MaxValue;

    public long MarkerOffset
    {
        get => _markerOffset;
        set
        {
            this.SetProperty(ref _markerOffset, value >= 0 ? value : 0);
            this.OnPropertyChanged(nameof(MarkerText));
            SetSignalMarkerValues(MarkerOffset);
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
            SetSignalMarkerValues(SecondMarkerOffset);
        }
    }

    private long _loadingMarkerOffset = long.MaxValue;

    public long LoadingMarkerOffset
    {
        get => _loadingMarkerOffset;
        set
        {
            SetProperty(ref _loadingMarkerOffset, value >= 0 ? value : 0);
            if (MarkerOffset == long.MaxValue) SetSignalMarkerValues(_loadingMarkerOffset);
        }
    }

    public string MarkerText
    {
        get
        {
            if (LoadingMarkerOffset != long.MaxValue)
            {
                if (MarkerOffset != long.MaxValue) return TimeHelper.ConvertNumber(MarkerOffset, TimeScale);
                return "?";
            }

            if (SecondMarkerOffset == long.MaxValue)
            {
                if (MarkerOffset != long.MaxValue) return TimeHelper.ConvertNumber(MarkerOffset, TimeScale);
                return "?";
            }

            if (MarkerOffset == long.MaxValue) return "?";
            var dist = SecondMarkerOffset - MarkerOffset;
            return (dist > 0 ? "+" : "") + TimeHelper.ConvertNumber(dist, TimeScale);
        }
    }

    public string CursorText => TimeHelper.ConvertNumber(CursorOffset, TimeScale);

    public event EventHandler? SignalRemoved;

    private void SetSignalMarkerValues(long offset)
    {
        foreach (var s in Signals)
        {
            SetSignalMarkerValue(s, offset);
        }
    }

    private void SetSignalMarkerValue(WaveModel model, long offset)
    {
        model.MarkerValue = SignalConverter.ConvertSignal(model.Signal.GetValueFromOffset(offset) ?? StdLogic.U, model);
    }

    public void ZoomIn()
    {
        ZoomLevel++;
    }

    public void ZoomOut()
    {
        ZoomLevel--;
    }

    public void ResetView()
    {
        Offset = 0;
        ZoomLevel = 0;
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

    public WaveModel AddSignal(IVcdSignal signal)
    {
        var waveModel = new WaveModel(signal, WaveColors[Signals.Count % WaveColors.Length]);
        SetSignalMarkerValue(waveModel, MarkerOffset);
        Signals.Add(waveModel);

        waveModel.WhenValueChanged(x => x.DataType).Subscribe(_ =>
        {
            SetSignalMarkerValue(waveModel, MarkerOffset);
        });
        
        return waveModel;
    }

    public void RemoveSignal(WaveModel model)
    {
        Signals.Remove(model);
        SignalRemoved?.Invoke(this, EventArgs.Empty);
    }
}