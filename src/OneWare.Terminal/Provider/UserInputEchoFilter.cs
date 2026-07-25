namespace OneWare.Terminal.Provider;

/// <summary>
/// Best-effort removal of locally echoed user keystrokes from the captured output of an
/// automation command. While an automation command runs, anything the user types into the
/// terminal is echoed back by the pty and would otherwise show up in the command's captured
/// output. This filter matches received bytes against recently sent user input and drops the
/// echo. It is only applied to the capture stream — the visible terminal is unaffected.
/// On any mismatch (echo disabled, interleaved program output) it stops guessing and passes
/// data through unchanged, so the worst failure mode is the old behavior.
/// </summary>
public sealed class UserInputEchoFilter
{
    private const int MaxPending = 4096;
    private static readonly TimeSpan EchoWindow = TimeSpan.FromMilliseconds(500);

    private readonly Queue<byte> _pending = new();
    private DateTimeOffset _lastInput = DateTimeOffset.MinValue;
    private bool _dropNextLf;

    /// <summary>Records user input written to the pty so its echo can be recognized.</summary>
    public void OnUserInput(byte[] data)
    {
        foreach (var b in data)
            _pending.Enqueue(b);

        while (_pending.Count > MaxPending)
            _pending.Dequeue();

        _lastInput = DateTimeOffset.Now;
    }

    /// <summary>Returns <paramref name="data"/> with pending user-input echo removed.</summary>
    public byte[] Filter(byte[] data)
    {
        if (_pending.Count == 0 && !_dropNextLf)
            return data;

        // Echo arrives within milliseconds of the keystroke. If the input is older than
        // that, it was not echoed (e.g. a password prompt) — never eat real output for it.
        if (_pending.Count > 0 && DateTimeOffset.Now - _lastInput > EchoWindow)
            _pending.Clear();

        var result = new List<byte>(data.Length);
        foreach (var b in data)
        {
            if (_dropNextLf)
            {
                _dropNextLf = false;
                if (b == (byte)'\n')
                    continue;
            }

            if (_pending.Count == 0)
            {
                result.Add(b);
                continue;
            }

            if (b == _pending.Peek())
            {
                _pending.Dequeue();
                // Enter is sent as CR but echoed as CRLF.
                if (b == (byte)'\r')
                    _dropNextLf = true;
                continue;
            }

            // The echo does not match — stop guessing and pass everything through.
            _pending.Clear();
            result.Add(b);
        }

        return result.ToArray();
    }
}
