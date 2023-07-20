namespace OneWare.WaveFormViewer.Models;

public class WavePart
{
    public long Time { get; }
    public object Data { get; }

    public WavePart(long time, object data)
    {
        Time = time;
        Data = data;
    }
}