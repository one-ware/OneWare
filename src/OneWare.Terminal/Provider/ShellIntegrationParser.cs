using System.Text;

namespace OneWare.Terminal.Provider;

/// <summary>
/// Streaming parser that extracts OneWare shell-integration sequences (OSC 633, terminated
/// by BEL or ST) from raw terminal output and passes everything else through unchanged.
/// The integration sequences are emitted invisibly by shell prompt hooks, so stripping them
/// here guarantees they never reach the user-facing terminal or captured command output.
/// The parser is chunk-safe: sequences may be split across any number of reads.
/// </summary>
public sealed class ShellIntegrationParser
{
    private const string OscIntegrationPrefix = "633;";
    // Long enough for the working directory property (OSC 633;P;Cwd=...).
    private const int MaxPayloadLength = 4096;

    private enum State
    {
        Text,
        Escape,
        OscPrefix,
        OscPayload,
        OscPayloadEscape
    }

    private readonly List<byte> _held = new();
    private readonly List<byte> _payload = new();
    private State _state = State.Text;
    private int _prefixMatched;

    /// <summary>
    /// A parsed piece of terminal output: either passthrough bytes or an integration event.
    /// Segments are returned in stream order so consumers can correlate output with events.
    /// </summary>
    public readonly record struct Segment(byte[]? Data, ShellIntegrationEvent? Event);

    public List<Segment> Feed(byte[] data)
    {
        var segments = new List<Segment>();
        var text = new List<byte>(data.Length);

        foreach (var b in data)
            Process(b, text, segments);

        FlushText(text, segments);
        return segments;
    }

    private void Process(byte b, List<byte> text, List<Segment> segments)
    {
        switch (_state)
        {
            case State.Text:
                if (b == 0x1B)
                {
                    _held.Add(b);
                    _state = State.Escape;
                }
                else
                {
                    text.Add(b);
                }

                break;

            case State.Escape:
                if (b == (byte)']')
                {
                    _held.Add(b);
                    _prefixMatched = 0;
                    _state = State.OscPrefix;
                }
                else
                {
                    // Not an OSC introducer: release the held ESC and reprocess this byte.
                    ReleaseHeld(text);
                    _state = State.Text;
                    Process(b, text, segments);
                }

                break;

            case State.OscPrefix:
                if (b == (byte)OscIntegrationPrefix[_prefixMatched])
                {
                    _held.Add(b);
                    _prefixMatched++;
                    if (_prefixMatched == OscIntegrationPrefix.Length)
                    {
                        _payload.Clear();
                        _state = State.OscPayload;
                    }
                }
                else
                {
                    // A different OSC sequence (window title, hyperlink, ...): pass it through.
                    ReleaseHeld(text);
                    text.Add(b);
                    _state = State.Text;
                }

                break;

            case State.OscPayload:
                if (b == 0x07)
                {
                    EmitEvent(text, segments);
                }
                else if (b == 0x1B)
                {
                    _held.Add(b);
                    _state = State.OscPayloadEscape;
                }
                else
                {
                    _held.Add(b);
                    _payload.Add(b);
                    if (_payload.Count > MaxPayloadLength)
                    {
                        // Runaway sequence without terminator: give up and pass it through.
                        ReleaseHeld(text);
                        _state = State.Text;
                    }
                }

                break;

            case State.OscPayloadEscape:
                if (b == (byte)'\\')
                {
                    EmitEvent(text, segments);
                }
                else
                {
                    _held.Add(b);
                    _payload.Add(0x1B);
                    _payload.Add(b);
                    _state = State.OscPayload;
                }

                break;
        }
    }

    private void EmitEvent(List<byte> text, List<Segment> segments)
    {
        FlushText(text, segments);
        segments.Add(new Segment(null, ShellIntegrationEvent.Parse(Encoding.UTF8.GetString(_payload.ToArray()))));
        _held.Clear();
        _payload.Clear();
        _state = State.Text;
    }

    private void ReleaseHeld(List<byte> text)
    {
        text.AddRange(_held);
        _held.Clear();
        _payload.Clear();
    }

    private static void FlushText(List<byte> text, List<Segment> segments)
    {
        if (text.Count == 0) return;
        segments.Add(new Segment(text.ToArray(), null));
        text.Clear();
    }
}

/// <summary>
/// A single OSC 633 shell-integration event, e.g. command started ("C"), command finished with
/// exit code ("D;0") or a property such as the working directory ("P;Cwd=/home/user").
/// </summary>
public readonly record struct ShellIntegrationEvent(char Command, string? Argument)
{
    public static ShellIntegrationEvent Parse(string payload)
    {
        if (payload.Length == 0) return new ShellIntegrationEvent('\0', null);

        var separator = payload.IndexOf(';');
        return separator < 0
            ? new ShellIntegrationEvent(payload[0], null)
            : new ShellIntegrationEvent(payload[0], payload[(separator + 1)..]);
    }

    /// <summary>
    /// The working directory reported by a "P;Cwd=..." event, or null for any other event.
    /// </summary>
    public string? WorkingDirectory =>
        Command == 'P' && Argument is { } argument && argument.StartsWith("Cwd=", StringComparison.Ordinal)
            ? UnescapeValue(argument[4..])
            : null;

    /// <summary>
    /// Reverses the escaping the shell scripts apply to property values: "\\" is a backslash and
    /// "\xHH" a character the sequence cannot carry literally (";", control characters).
    /// </summary>
    public static string UnescapeValue(string value)
    {
        if (!value.Contains('\\')) return value;

        var result = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                if (value[i + 1] == '\\')
                {
                    result.Append('\\');
                    i++;
                    continue;
                }

                if (value[i + 1] == 'x' && i + 3 < value.Length &&
                    byte.TryParse(value.AsSpan(i + 2, 2), System.Globalization.NumberStyles.HexNumber, null,
                        out var code))
                {
                    result.Append((char)code);
                    i += 3;
                    continue;
                }
            }

            result.Append(value[i]);
        }

        return result.ToString();
    }
}
