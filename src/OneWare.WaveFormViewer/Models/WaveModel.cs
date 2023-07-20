using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
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
    
    public string Label { get; }
    public IBrush WaveBrush { get; }
    public SignalLineType LineType { get; }
    public SignalDataType DataType => SignalDataType.Decimal;
    public List<WavePart> Line { get; }

    public WaveModel(string label, SignalLineType lineType, List<WavePart> line, IBrush waveBrush)
    {
        Label = label;
        LineType = lineType;
        Line = line;
        WaveBrush = waveBrush;
    }
}