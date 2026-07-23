using System.Collections.Generic;
using VtNetCore.Avalonia;

namespace OneWare.Terminal.Provider;

public sealed class OutputSequenceSuppressor : IOutputFilter, IOutputSuppressor
{
    private const int TolerantSuppressionPrefixLength = 4;
    private readonly object _lock = new();
    private readonly Queue<byte[]> _queue = new();
    private readonly List<byte> _pending = new();
    private byte[]? _active;
    private int _matchedLength;

    public void SuppressOutput(byte[] sequence)
    {
        if (sequence.Length == 0) return;

        lock (_lock)
        {
            _queue.Enqueue(sequence);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _queue.Clear();
            ResetActiveSuppression();
        }
    }

    public byte[] FilterOutput(byte[] data)
    {
        if (data.Length == 0) return data;

        lock (_lock)
        {
            if (_active == null && _queue.Count == 0) return data;

            var output = new List<byte>(data.Length + 8);
            var index = 0;

            while (index < data.Length)
            {
                if (_active == null && _queue.Count > 0)
                {
                    _active = _queue.Dequeue();
                    _pending.Clear();
                    _matchedLength = 0;
                }

                if (_active == null)
                {
                    output.Add(data[index++]);
                    continue;
                }

                var b = data[index++];
                if (b == _active[_matchedLength])
                {
                    _pending.Add(b);
                    _matchedLength++;
                    if (_matchedLength == _active.Length)
                        ResetActiveSuppression();

                    continue;
                }

                if (_matchedLength >= Math.Min(TolerantSuppressionPrefixLength, _active.Length))
                {
                    // Interactive shells may insert cursor movement and redraw bytes while
                    // echoing a command that wraps. Match the command as a subsequence.
                    _pending.Add(b);
                    if (b == (byte)'\n')
                    {
                        output.AddRange(_pending);
                        ResetActiveSuppression();
                    }

                    continue;
                }

                if (_pending.Count > 0)
                {
                    output.AddRange(_pending);
                    _pending.Clear();
                    _matchedLength = 0;
                }

                if (b == _active[0])
                {
                    _pending.Add(b);
                    _matchedLength = 1;
                    if (_active.Length == 1)
                        ResetActiveSuppression();
                }
                else
                {
                    output.Add(b);
                }
            }

            return output.Count == data.Length ? data : output.ToArray();
        }
    }

    private void ResetActiveSuppression()
    {
        _active = null;
        _pending.Clear();
        _matchedLength = 0;
    }
}
