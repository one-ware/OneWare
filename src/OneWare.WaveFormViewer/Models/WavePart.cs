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

    public WavePart AddTime(long time)
    {
        return new WavePart(Time + time, Data);
    }
}