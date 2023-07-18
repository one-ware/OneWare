namespace OneWare.WaveFormViewer.Models;

public class WavePart
{
    public long Time { get; }
    public string Data { get; }

    public WavePart(long time, string data)
    {
        Time = time;
        Data = data;
    }

    public WavePart AddTime(long time)
    {
        return new WavePart(Time + time, Data);
    }
}