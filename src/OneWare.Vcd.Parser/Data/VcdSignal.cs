using OneWare.Vcd.Parser.Extensions;

namespace OneWare.Vcd.Parser.Data;

public class VcdSignal<T> : IVcdSignal
{
    public List<long> ChangeTimes { get; }
    public VcdLineType Type { get; }
    public int BitWidth { get; }
    public char Id { get; }
    public string Name { get; }

    private readonly List<int> _changeTimeOffsets = new();
    private readonly List<T?> _values = new();

    public event EventHandler RequestRedraw;

    public VcdSignal(List<long> changeTimes, VcdLineType type, int bitWidth, char id, string name)
    {
        ChangeTimes = changeTimes;
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
    }
    
    public void AddChange(int timeIndex, object change)
    {
        _changeTimeOffsets.Add(timeIndex);
        _values.Add((T)change);
    }

    public void Clear()
    {
        _changeTimeOffsets.Clear();
        _changeTimeOffsets.TrimExcess();
        _values.Clear();
        _values.TrimExcess();
    }
    
    public int FindIndex(long offset)
    {
        var result = _changeTimeOffsets.BinarySearchNext(offset, (l, left, right) =>
        {
            if (left == right) return 0;
            var leftOffset = ChangeTimes[left];
            var rightOffset = ChangeTimes[right];
            if (l >= leftOffset && l < rightOffset) return 0;
            if (l > leftOffset) return 1;
            return -1;
        });

        return result;
    }

    private T? GetValue(long offset)
    {
        var index = FindIndex(offset);

        return index >= 0 ? _values[index] : default;
    }
    
    public object? GetValueFromOffset(long offset)
    {
        return GetValue(offset) as object;
    }

    public long GetChangeTimeFromIndex(int index)
    {
        return index < _changeTimeOffsets.Count ? ChangeTimes[_changeTimeOffsets[index]] : long.MaxValue;
    }
    
    public object? GetValueFromIndex(int index)
    {
        return index < _values.Count ? _values[index] : null;
    }

    public void Invalidate()
    {
        RequestRedraw?.Invoke(null, EventArgs.Empty);
    }
}