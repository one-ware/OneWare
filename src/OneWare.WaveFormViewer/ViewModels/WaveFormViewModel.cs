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
    private static readonly IBrush[] WaveColors =
        [Brushes.Lime, Brushes.Cyan, Brushes.Magenta, Brushes.Yellow];

    private long _cursorOffset;

    private bool _extendSignals;

    private long _loadingMarkerOffset = long.MaxValue;

    private long _markerOffset = long.MaxValue;

    private long _max;

    private long _offset;

    private long _secondMarkerOffset = long.MaxValue;

    private long _timeScale = 1;

    private int _zoomLevel;
    private double _zoomMultiply = 1;
    public int MinZoomLevel { get; } = -2;
    public int MaxZoomLevel { get; } = 20;

    public ObservableCollection<WaveModel> Signals { get; } = [];

    /// <summary>
    ///     1 = 1 fs
    /// </summary>
    public long TimeScale
    {
        get => _timeScale;
        set
        {
            SetProperty(ref _timeScale, value);
            TimeScaleUnit = TimeHelper.GetTimeScaleFromUnit(TimeScale);
        }
    }

    public string TimeScaleUnit { get; private set; } = string.Empty;

    public bool ExtendSignals
    {
        get => _extendSignals;
        set => SetProperty(ref _extendSignals, value);
    }

    public long Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, value > 0 ? value : 0);
    }

    public long ViewPortWidth => (long)(Max / ZoomMultiply);

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
                if (MarkerOffset != long.MaxValue)
                    Offset = MarkerOffset - currentOffsetWidth / 2;
            }
        }
    }

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

    public long CursorOffset
    {
        get => _cursorOffset;
        set
        {
            SetProperty(ref _cursorOffset, value >= 0 ? value : 0);
            OnPropertyChanged(nameof(CursorText));
        }
    }

    public long MarkerOffset
    {
        get => _markerOffset;
        set
        {
            SetProperty(ref _markerOffset, value >= 0 ? value : 0);
            OnPropertyChanged(nameof(MarkerText));
            OnPropertyChanged(nameof(MarkerTextOriginal));
            SetSignalMarkerValues(MarkerOffset);
        }
    }

    public long SecondMarkerOffset
    {
        get => _secondMarkerOffset;
        set
        {
            SetProperty(ref _secondMarkerOffset, value >= 0 ? value : 0);
            OnPropertyChanged(nameof(MarkerText));
            OnPropertyChanged(nameof(MarkerTextOriginal));
            SetSignalMarkerValues(SecondMarkerOffset);
        }
    }

    public long LoadingMarkerOffset
    {
        get => _loadingMarkerOffset;
        set
        {
            SetProperty(ref _loadingMarkerOffset, value >= 0 ? value : 0);
            if (MarkerOffset == long.MaxValue) SetSignalMarkerValues(_loadingMarkerOffset);
        }
    }

    public string MarkerTextOriginal
    {
        get
        {
            if (LoadingMarkerOffset != long.MaxValue)
            {
                if (MarkerOffset != long.MaxValue) return $"{MarkerOffset} {TimeScaleUnit}";
                return "?";
            }

            if (SecondMarkerOffset == long.MaxValue)
            {
                if (MarkerOffset != long.MaxValue) return $"{MarkerOffset} {TimeScaleUnit}";
                return "?";
            }

            if (MarkerOffset == long.MaxValue) return "?";
            var dist = SecondMarkerOffset - MarkerOffset;
            return (dist > 0 ? "+" : "") + $"{dist} {TimeScaleUnit}";
        }
    }
    
    public string MarkerText
    {
        get
        {
            if (MarkerOffset != long.MaxValue && (LoadingMarkerOffset != long.MaxValue || SecondMarkerOffset == long.MaxValue))
            {
                return TimeHelper.FormatTime(MarkerOffset, TimeScale, ViewPortWidth);
            }

            if (MarkerOffset == long.MaxValue) return "?";
            
            var dist = SecondMarkerOffset - MarkerOffset;
            return (dist > 0 ? "+" : "") + TimeHelper.FormatTime(dist, TimeScale, ViewPortWidth);
        }
    }

    public string CursorText => TimeHelper.FormatTime(CursorOffset, TimeScale, ViewPortWidth);

    public event EventHandler? SignalRemoved;

    private void SetSignalMarkerValues(long offset)
    {
        foreach (var s in Signals) SetSignalMarkerValue(s, offset);
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
        ZoomLevel = 0;
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

    public WaveModel AddSignal(IVcdSignal signal)
    {
        var waveModel = new WaveModel(signal, WaveColors[Signals.Count % WaveColors.Length]);
        SetSignalMarkerValue(waveModel, MarkerOffset);
        Signals.Add(waveModel);

        waveModel.WhenValueChanged(x => x.DataType).Subscribe(_ => { SetSignalMarkerValue(waveModel, MarkerOffset); });

        return waveModel;
    }

    public void RemoveSignal(WaveModel model)
    {
        Signals.Remove(model);
        SignalRemoved?.Invoke(this, EventArgs.Empty);
    }
}