using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Parser.Extensions;
using OneWare.Vcd.Parser.Helpers;

namespace OneWare.Vcd.Parser;

public static partial class VcdParser
{
    private const int ThreadFixOffset = 1000;

    private const int MaxDefinitionSize = 50000;
    private const int BufferSize = 1024;

    [GeneratedRegex("(\\d+)\\s?(s|ms|us|ns|ps|fs)")]
    private static partial Regex TimeScaleRegex();

    public static VcdFile ParseVcdDefinition(string path, object? parseLock = null)
    {
        using var stream =
            new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.ReadWrite); // File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        var vcdFile = ReadDefinition(reader, parseLock);

        return vcdFile;
    }

    public static async Task ReadSignalsAsync(string path, VcdFile vcdFile,
        IProgress<(int thread, int progress)> progress, CancellationToken cancellationToken, int threads,
        object? parseLock = null)
    {
        //Use thread safe variant
        if (threads == 1)
        {
            await Task.Run(async () =>
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.Read | FileShare.ReadWrite);
                stream.Seek(vcdFile.DefinitionParseEndPosition + 1, SeekOrigin.Begin);
                var reader = new StreamReader(stream);

                await ReadSignals(reader, vcdFile.Definition.SignalRegister, vcdFile.Definition.ChangeTimes, parseLock,
                    new Progress<int>(x => progress.Report((0, x))),
                    cancellationToken);
                if (!cancellationToken.IsCancellationRequested &&
                    RuntimeInformation.OSArchitecture is not Architecture.Wasm) await Task.Delay(1, cancellationToken);
                reader.Dispose();
            }, cancellationToken);
            return;
        }

        var info = new FileInfo(path);
        var remainingLength = info.Length - vcdFile.DefinitionParseEndPosition;

        var partLength = remainingLength / threads;

        var tasks = new List<Task<(List<long> times, Dictionary<string, IVcdSignal> signals)>>();

        var threadC = 0;
        var threadFix = ThreadFixOffset;
        for (var i = vcdFile.DefinitionParseEndPosition + 1; i < info.Length; i += partLength)
        {
            var begin = i;
            var length = partLength;
            if (threadC > 0)
            {
                begin -= threadFix;
                length += threadFix;
            }

            if (begin < vcdFile.DefinitionParseEndPosition + 1) begin = vcdFile.DefinitionParseEndPosition + 1;
            if (begin + length > info.Length) length = info.Length - begin;

            tasks.Add(ReadSignalsPart(path, begin, length, vcdFile, threadC, progress, cancellationToken));
            threadC++;
        }

        var results = await Task.WhenAll(tasks);

        foreach (var r in results.OrderBy(x => x.times.LastOrDefault()))
        {
            //Sometimes when loading the file in multiple threads at the same time, blocks get cut off before being read completely.
            //That's why we implemented the threadFixOffset, which makes every part read the end of the last part again.
            //In this while loop we filter out the duplicates
            while (vcdFile.Definition.ChangeTimes.Any() &&
                   vcdFile.Definition.ChangeTimes.Last() >= r.times.FirstOrDefault())
            {
                foreach (var signal in vcdFile.Definition.SignalRegister)
                    signal.Value.RemoveChangeAtIndex(vcdFile.Definition.ChangeTimes.Count - 1);

                vcdFile.Definition.ChangeTimes.Remove(vcdFile.Definition.ChangeTimes.Last());
            }

            foreach (var signal in r.signals) vcdFile.Definition.SignalRegister[signal.Key].AddChanges(signal.Value);

            vcdFile.Definition.ChangeTimes.AddRange(r.times);
            r.signals.Clear();
            r.signals.TrimExcess();
            r.times.Clear();
            r.times.TrimExcess();
        }
    }

    private static Task<(List<long> times, Dictionary<string, IVcdSignal> signals)> ReadSignalsPart(string path,
        long begin, long length, VcdFile file,
        int threadId, IProgress<(int thread, int progress)> progress, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await using var stream =
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
            stream.Seek(begin, SeekOrigin.Begin);
            await using var limitStream = new StreamReadLimitLengthWrapper(stream, length);
            var reader = new StreamReader(limitStream);

            var signals = file.Definition.SignalRegister
                .ToDictionary(f => f.Key, f => f.Value.CloneEmpty());

            var changeTimes = new List<long>();

            await ReadSignals(reader, signals, changeTimes, null,
                new Progress<int>(x => { progress.Report((threadId, x)); }), cancellationToken);

            reader.Dispose();
            return (changeTimes, signals);
        }, cancellationToken);
    }

    public static Task<VcdFile> ParseVcdAsync(string path)
    {
        return Task.Run(() => ParseVcd(path));
    }

    public static VcdFile ParseVcd(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
        return ParseVcd(stream);
    }

    private static VcdFile ParseVcd(Stream stream, object? parseLock = null)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        var vcdFile = ReadDefinition(reader, parseLock);

        ReadSignals(reader, vcdFile.Definition.SignalRegister, vcdFile.Definition.ChangeTimes).Wait();

        return vcdFile;
    }

    private static VcdFile ReadDefinition(TextReader reader, object? parseLock)
    {
        var definition = new VcdDefinition();
        IScopeHolder currentScope = definition;
        string? keyWord = null;
        var words = new List<string>();

        var vcdFile = new VcdFile(definition)
        {
            DefinitionParseEndPosition = reader.ProcessWords(MaxDefinitionSize, x =>
            {
                if (x.StartsWith('$') && x.Length > 3)
                {
                    switch (x)
                    {
                        case "$end":
                            switch (keyWord)
                            {
                                case "$timescale":
                                    definition.TimeScale = ParseTimeScale(string.Join(' ', words));
                                    break;
                                case "$var":
                                    if (words.Count >= 4)
                                    {
                                        var type = Enum.Parse<VcdLineType>(words[0], true);

                                        var bitWith = int.Parse(words[1]);
                                        var id = words[2];
                                        var name = words[3];

                                        if (definition.SignalRegister.TryGetValue(id, out var existing))
                                        {
                                            currentScope.Signals.Add(existing);
                                            break;
                                        }

                                        IVcdSignal signal = type switch
                                        {
                                            VcdLineType.Real => bitWith switch
                                            {
                                                32 => new VcdSignal<float>(definition.ChangeTimes, type, bitWith, id,
                                                    name, parseLock),
                                                64 => new VcdSignal<double>(definition.ChangeTimes, type, bitWith, id,
                                                    name, parseLock),
                                                _ => throw new Exception(
                                                    $"Invalid VCD Definition: BitWidth of REG {name} is not 32 or 64: {bitWith}")
                                            },
                                            _ => bitWith switch
                                            {
                                                1 => new VcdSignal<StdLogic>(definition.ChangeTimes, type, bitWith, id,
                                                    name, parseLock),
                                                _ => new VcdSignal<StdLogic[]>(definition.ChangeTimes, type, bitWith,
                                                    id, name, parseLock)
                                            }
                                        };

                                        currentScope.Signals.Add(signal);
                                        definition.SignalRegister.TryAdd(signal.Id, signal);
                                    }

                                    break;
                                case "$scope":
                                    var newScope = new VcdScope(currentScope, string.Join(' ', words));
                                    currentScope.Scopes.Add(newScope);
                                    currentScope = newScope;
                                    break;
                                case "$upscope":
                                    currentScope = currentScope.Parent ?? throw new Exception("Invalid VCD Definition");
                                    break;
                                case "$enddefinitions":
                                    return false;
                            }

                            keyWord = null;
                            break;
                    }

                    keyWord = x;
                    words.Clear();
                }
                else
                {
                    words.Add(x);
                }

                return true;
            })
        };

        return vcdFile;
    }

    private static long ParseTimeScale(string ts)
    {
        var match = TimeScaleRegex().Match(ts);

        if (!match.Success) throw new Exception("Invalid time scale definition");
        var time = match.Groups[1].Value;
        var unit = match.Groups[2].Value;

        return unit switch
        {
            "fs" => long.Parse(time),
            "ps" => long.Parse(time) * 1000,
            "ns" => long.Parse(time) * 1000_000,
            "us" => long.Parse(time) * 1000_000_000,
            "ms" => long.Parse(time) * 1000_000_000_000,
            "s" => long.Parse(time) * 1000_000_000_000_000,
            _ => throw new Exception("Invalid time scale unit")
        };
    }

    public static async Task<long?> TryFindLastTime(string path, int backOffset = 1000)
    {
        await using var stream =
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
        var result = await TryFindLastTime(stream, backOffset);
        return result;
    }

    public static async Task<long?> TryFindLastTime(Stream stream, int backOffset = 500)
    {
        if (stream.Length < backOffset)
            backOffset = (int)stream.Length;

        stream.Seek(-backOffset, SeekOrigin.End);
        using var reader = new StreamReader(stream);

        var text = await reader.ReadToEndAsync();

        var lines = text.Split('\n');
        var lastTime = lines.LastOrDefault(x => x.StartsWith('#'));
        if (!string.IsNullOrWhiteSpace(lastTime))
            if (long.TryParse(lastTime.Trim()[1..], out var time))
                return time;

        return null;
    }

    private static async Task ReadSignals(StreamReader reader, IReadOnlyDictionary<string, IVcdSignal> signalRegister,
        ICollection<long> changeTimes, object? parseLock = null,
        IProgress<int>? progress = null, CancellationToken? cancellationToken = default)
    {
        parseLock ??= new object();

        var currentTime = 0L;
        var currentReal = string.Empty;
        var currentLogic = StdLogic.U;
        var addedTime = false;
        var emptyTime = true;
        var currentVector = new List<StdLogic>();

        var lastC = '\n';
        var parsingPos = ParsingPosition.None;
        var parsingSignalType = ParsingType.Array;

        var idBuilder = new StringBuilder();

        /*  Example Block
            #78083021000\r\n
            0#\r\n
            0$\r\n
        */

        long? progressSnap = progress != null ? (reader.BaseStream.Length - reader.BaseStream.Position) / 100 : null;
        var progressC = 0;
        long counter = 0;

        while (!reader.EndOfStream)
        {
            if (cancellationToken is { IsCancellationRequested: true }) return;

            var c = (char)reader.Read();

            if (reader.EndOfStream)
                //Wait for new input from simulator
                if (RuntimeInformation.OSArchitecture is not Architecture.Wasm)
                    await Task.Delay(50);

            if (progress != null)
            {
                counter++;

                if (counter > progressSnap)
                {
                    progressC++;
                    progress.Report(progressC);
                    counter = 0;
                }
            }

            switch (parsingPos)
            {
                case ParsingPosition.None:

                    if (lastC is '\n')
                        switch (c)
                        {
                            case 'U':
                                if (!addedTime) break;
                                currentLogic = StdLogic.U;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'X':
                                if (!addedTime) break;
                                currentLogic = StdLogic.X;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case '0':
                                if (!addedTime) break;
                                currentLogic = StdLogic.Zero;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case '1':
                                if (!addedTime) break;
                                currentLogic = StdLogic.Full;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'Z':
                                if (!addedTime) break;
                                currentLogic = StdLogic.Z;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'W':
                                if (!addedTime) break;
                                currentLogic = StdLogic.W;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'L':
                                if (!addedTime) break;
                                currentLogic = StdLogic.L;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'H':
                                if (!addedTime) break;
                                currentLogic = StdLogic.H;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case '-':
                                if (!addedTime) break;
                                currentLogic = StdLogic.DontCare;
                                parsingSignalType = ParsingType.Logic;
                                parsingPos = ParsingPosition.Id;
                                break;
                            case 'b':
                                if (!addedTime) break;
                                parsingSignalType = ParsingType.Array;
                                parsingPos = ParsingPosition.Value;
                                currentVector.Clear();
                                break;
                            case 'r':
                                if (!addedTime) break;
                                currentReal = string.Empty;
                                parsingSignalType = ParsingType.Real;
                                parsingPos = ParsingPosition.Value;
                                break;
                            case '#':
                                currentTime = 0L;
                                parsingPos = ParsingPosition.Time;
                                emptyTime = true;
                                break;
                        }

                    break;

                case ParsingPosition.Value:
                    if (!addedTime) continue;
                    switch (parsingSignalType)
                    {
                        case ParsingType.Array:
                            switch (c)
                            {
                                case 'U':
                                case 'X':
                                case '0':
                                case '1':
                                case 'Z':
                                case 'W':
                                case 'L':
                                case 'H':
                                case '-':
                                    currentVector.Add(StdLogicHelpers.ParseLogic(c));
                                    break;
                                case ' ':
                                    parsingPos = ParsingPosition.Id;
                                    break;
                            }

                            break;
                        case ParsingType.Real:
                            switch (c)
                            {
                                case ' ':
                                    parsingPos = ParsingPosition.Id;
                                    break;
                                default:
                                    currentReal += c;
                                    break;
                            }

                            break;
                        case ParsingType.Logic:

                            break;
                    }

                    break;

                case ParsingPosition.Id:
                    switch (c)
                    {
                        case '\r':
                        case '\n':

                            var id = idBuilder.ToString();
                            idBuilder.Clear();

                            switch (parsingSignalType)
                            {
                                case ParsingType.Logic:
                                    signalRegister[id].AddChange(changeTimes.Count - 1, currentLogic);
                                    break;
                                case ParsingType.Array when signalRegister[id].ValueType == typeof(StdLogic[]):
                                    if (currentVector.Count < signalRegister[id].BitWidth)
                                    {
                                        var padCount = signalRegister[id].BitWidth - currentVector.Count;
                                        var padding = Enumerable.Repeat(StdLogic.Zero, padCount).ToList();
                                        currentVector.InsertRange(0, padding);
                                    }
                                    signalRegister[id].AddChange(changeTimes.Count - 1, currentVector.ToArray());
                                    break;
                                case ParsingType.Array when signalRegister[id].ValueType == typeof(StdLogic) && currentVector.Count == 1:
                                    signalRegister[id].AddChange(changeTimes.Count - 1, currentVector[0]);
                                    break;
                                case ParsingType.Real:
                                    switch (signalRegister[id])
                                    {
                                        case VcdSignal<float>:
                                            if (float.TryParse(currentReal, NumberStyles.Float,
                                                    CultureInfo.InvariantCulture, out var result))
                                                signalRegister[id].AddChange(changeTimes.Count - 1, result);
                                            else
                                                throw new Exception("Parsing failed: " + currentReal);
                                            break;
                                        case VcdSignal<double>:
                                            if (double.TryParse(currentReal, NumberStyles.Float,
                                                    CultureInfo.InvariantCulture, out var result2))
                                                signalRegister[id].AddChange(changeTimes.Count - 1, result2);
                                            else
                                                throw new Exception("Parsing failed: " + currentReal);
                                            break;
                                    }

                                    break;
                            }

                            parsingPos = ParsingPosition.None;
                            break;
                        default:
                            idBuilder.Append(c);
                            break;
                    }

                    break;

                case ParsingPosition.Time:
                    switch (c)
                    {
                        case '\r':
                        case '\n':
                            if (!emptyTime)
                            {
                                lock (parseLock)
                                {
                                    changeTimes.Add(currentTime);
                                }

                                addedTime = true;
                            }

                            parsingPos = ParsingPosition.None;
                            break;
                        default:
                            currentTime = AddNumber(currentTime, c);
                            emptyTime = false;
                            break;
                    }

                    break;
                default:
                    throw new Exception("Unexpected Character");
            }

            lastC = c;
        }

        progress?.Report(100);
    }

    private static long AddNumber(long n, char c)
    {
        if (c is >= '0' and <= '9') return n * 10 + (c - '0');

        throw new FormatException("Invalid time parsing");
    }

    private enum ParsingPosition
    {
        None,
        Time,
        Value,
        Id
    }

    private enum ParsingType
    {
        Logic,
        Array,
        Real
    }
}