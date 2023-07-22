using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Vcd.Parser.Data;
using OneWare.WaveFormViewer.Enums;

namespace OneWare.WaveFormViewer.Models;

public class WaveModel : ObservableObject
{
    private string? _markerValue;
    public string? MarkerValue
    {
        get => _markerValue;
        set => SetProperty(ref _markerValue, value);
    }
    public IBrush WaveBrush { get; }
    public SignalDataType DataType => SignalDataType.Decimal;
    public IVcdSignal Signal { get; }

    public WaveModel(IVcdSignal signal, IBrush waveBrush)
    {
        Signal = signal;
        WaveBrush = waveBrush;
    }
}