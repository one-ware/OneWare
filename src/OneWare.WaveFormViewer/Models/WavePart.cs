namespace OneWare.WaveFormViewer.Models;

public record struct WavePart
{
    public long Time { get; init; }
    public object Data { get; init; }
}