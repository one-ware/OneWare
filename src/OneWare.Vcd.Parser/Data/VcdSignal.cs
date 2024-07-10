using System.Collections;
using OneWare.Vcd.Parser.Extensions;

namespace OneWare.Vcd.Parser.Data;

public class VcdSignal<T> : IVcdSignal
{
    public List<long> ChangeTimes { get; }
    public Type ValueType => typeof(T);
    public VcdLineType Type { get; }
    public int BitWidth { get; }
    public string Id { get; }
    public string Name { get; }

    private readonly List<int> _changeTimeOffsets = new();
    private readonly List<T> _values = new();

    public event EventHandler? RequestRedraw;

    private readonly object _parseLock;

    public VcdSignal(List<long> changeTimes, VcdLineType type, int bitWidth, string id, string name, object? parseLock = null)
    {
        ChangeTimes = changeTimes;
        Type = type;
        BitWidth = bitWidth;
        Id = id;
        Name = name;
        _parseLock = parseLock ?? new object();
    }
    
    public void AddChange(int timeIndex, dynamic change)
    {
        lock (_parseLock)
        {
            if (typeof(T).IsArray && _values.Count > 0)
            {
                if(Enumerable.SequenceEqual(_values[^1], change)) return;
            }
            _changeTimeOffsets.Add(timeIndex);
            _values.Add((T)change);
        }
    }
    
    public void AddChanges(IVcdSignal signal)
    {
        lock (_parseLock)
        {
            if (signal is not VcdSignal<T> s) return;
            var offset = ChangeTimes.Count;
            foreach (var off in s._changeTimeOffsets)
            {
                _changeTimeOffsets.Add(off+offset);
            }
            _values.AddRange(s._values);
        }
    }

    public void RemoveChangeAtIndex(int changeTimeIndex)
    {
        lock (_parseLock)
        {
            var changeTimeOffsetIndex = _changeTimeOffsets.IndexOf(changeTimeIndex);
            if(changeTimeOffsetIndex < 0) return;
            _changeTimeOffsets.RemoveAt(changeTimeOffsetIndex);
            _values.RemoveAt(changeTimeOffsetIndex);
        }
    }

    public void Clear()
    {
        lock (_parseLock)
        {
            _changeTimeOffsets.Clear();
            _changeTimeOffsets.TrimExcess();
            _values.Clear();
            _values.TrimExcess();
        }
    }
    
    public int FindIndex(long offset)
    {
        lock (_parseLock)
        {
            var result = _changeTimeOffsets.BinarySearchNext(offset, (l, left, right) =>
            {
                //if (left == right) return 0;
                var leftOffset = ChangeTimes[left];
                var rightOffset = ChangeTimes[right];
                if (l >= leftOffset && l < rightOffset) return 0;
                if (l > leftOffset) return 1;
                return -1;
            });

            if (result < 0)
            {
                if (_changeTimeOffsets.Count > 0 && ChangeTimes[_changeTimeOffsets.Last()] <= offset)
                    result = _changeTimeOffsets.Count - 1;
                else if(_changeTimeOffsets.Count == 1 && ChangeTimes[_changeTimeOffsets[0]] == offset)
                    result = 0;
            }

            return result;
        }
    }

    private T? GetValue(long offset)
    {
        var index = FindIndex(offset);

        return index >= 0 && index < _values.Count ? _values[index] : default;
    }
    
    public object? GetValueFromOffset(long offset)
    {
        return GetValue(offset);
    }

    public long GetChangeTimeFromIndex(int index)
    {
        lock (_parseLock)
        {
            return index < _changeTimeOffsets.Count ? ChangeTimes[_changeTimeOffsets[index]] : long.MaxValue;
        }
    }
    
    public object? GetValueFromIndex(int index)
    {
        return index < _values.Count ? _values[index] : null;
    }

    public void Invalidate()
    {
        RequestRedraw?.Invoke(null, EventArgs.Empty);
    }

    public IVcdSignal CloneEmpty()
    {
        return new VcdSignal<T>(new List<long>(), Type, BitWidth, Id, Name);
    }
}