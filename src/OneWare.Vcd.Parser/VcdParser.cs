using System.Text;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Parser.Extensions;

namespace OneWare.Vcd.Parser;

public static class VcdParser
{
    private enum ParsingPosition
    {
        None,
        Time,
        Signal
    }
    
    private const int MaxDefinitionSize = 10000;
    private const int BufferSize = 1024;

    public static VcdFile ParseVcdDefinition(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);// File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        var vcdFile = ReadDefinition(reader);

        return vcdFile;
    }
    
    public static async Task ReadSignalsAsync(string path, VcdFile vcdFile, IProgress<(int thread, int progress)> progress, CancellationToken cancellationToken, int threads)
    {
        //Use thread safe variant
        if (threads == 1) 
        {
            await Task.Run(async () =>
            {
                await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
                stream.Seek(vcdFile.DefinitionParseEndPosition+1, SeekOrigin.Begin);
                var reader = new StreamReader(stream);
                await ReadSignals(reader, vcdFile.Definition.SignalRegister, vcdFile.Definition.ChangeTimes, 
                    new Progress<int>(x => progress.Report((0, x))), 
                    cancellationToken);
                if(!cancellationToken.IsCancellationRequested) await Task.Delay(1, cancellationToken);
                reader.Dispose();
            });
            return;
        }
        
        var info = new FileInfo(path);
        var remainingLength = info.Length - vcdFile.DefinitionParseEndPosition;
        
        var partLength = remainingLength / threads;

        var tasks = new List<Task<(List<long> times, Dictionary<char, IVcdSignal> signals)>>();

        var threadC = 0;
        for (var i = vcdFile.DefinitionParseEndPosition + 1; i < info.Length; i += partLength)
        {
            tasks.Add(ReadSignalsPart(path, i, partLength, vcdFile, threadC, progress, cancellationToken));
            threadC++;
        }
        var results = await Task.WhenAll(tasks);

        foreach (var r in results.OrderBy(x => x.times.LastOrDefault()))
        {
            foreach (var signal in r.signals)
            {
                vcdFile.Definition.SignalRegister[signal.Key].AddChanges(signal.Value);
            }
            vcdFile.Definition.ChangeTimes.AddRange(r.times);
            r.signals.Clear();
            r.signals.TrimExcess();
            r.times.Clear();
            r.times.TrimExcess();
        }
    }

    private static Task<(List<long> times, Dictionary<char, IVcdSignal> signals)> ReadSignalsPart(string path, long begin, long length, VcdFile file, 
        int threadId, IProgress<(int thread, int progress)> progress, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
            stream.Seek(begin, SeekOrigin.Begin);
            await using var limitStream = new StreamReadLimitLengthWrapper(stream, length);
            var reader = new StreamReader(limitStream);

            var signals = file.Definition.SignalRegister
                .ToDictionary(f => f.Key, f => f.Value.CloneEmpty());

            var changeTimes = new List<long>();
            
            await ReadSignals(reader, signals, changeTimes, new Progress<int>(x =>
            {
                progress.Report((threadId, x));
            }), cancellationToken);
            
            reader.Dispose();
            return (changeTimes, signals);
        });
    }
    
    public static Task<VcdFile> ParseVcdAsync(string path)
    {
        return Task.Run(() => ParseVcd(path));
    }

    public static VcdFile ParseVcd(string path)
    {
        using var stream =  new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
        return ParseVcd(stream);
    }

    public static VcdFile ParseVcd(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);

        var vcdFile = ReadDefinition(reader);

        ReadSignals(reader, vcdFile.Definition.SignalRegister, vcdFile.Definition.ChangeTimes).Wait();

        return vcdFile;
    }

    private static VcdFile ReadDefinition(TextReader reader)
    {
        var definition = new VcdDefinition();
        IScopeHolder currentScope = definition;
        string? keyWord = null;
        var words = new List<string>();

        var vcdFile = new VcdFile(definition)
        {
            DefinitionParseEndPosition = reader.ProcessWords(MaxDefinitionSize, x =>
            {
                if (x.StartsWith('$') && x.Length > 1)
                {
                    switch (x)
                    {
                        case "$end":
                            switch (keyWord)
                            {
                                case "$timescale":
                                    definition.TimeScale = string.Join(' ', words);
                                    break;
                                case "$var":
                                    if (words.Count == 4)
                                    {
                                        var type = Enum.Parse<VcdLineType>(words[0], true);

                                        IVcdSignal signal = type switch
                                        {
                                            VcdLineType.Reg => new VcdSignal<byte>(definition.ChangeTimes, type, int.Parse(words[1]), words[2][0], words[3]),
                                            VcdLineType.Integer => new VcdSignal<int>(definition.ChangeTimes, type, int.Parse(words[1]), words[2][0], words[3]),
                                            _ => new VcdSignal<object>(definition.ChangeTimes, type, int.Parse(words[1]), words[2][0], words[3]),
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
    
    public static async Task<long?> TryFindLastTime(string path, int backOffset = 1000)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.ReadWrite);
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
        var lastTime = lines.Last(x => x.StartsWith('#'));
        if (!string.IsNullOrWhiteSpace(lastTime))
        {
            if(long.TryParse(lastTime.Trim()[1..], out var time)) return time;
        }
        return null;
    }
    
    private static async Task ReadSignals(StreamReader reader, IReadOnlyDictionary<char, IVcdSignal> signalRegister, 
        ICollection<long> changeTimes,
        IProgress<int>? progress = null, CancellationToken? cancellationToken = default)
    {
        var currentTime = 0L;
        var currentInteger = 0;
        var addedTime = false;

        var lastC = '\n';
        var parsingPos = ParsingPosition.None;
        var parsingSignalType = VcdLineType.Reg;

        /*  Example Block
            #78083021000\r\n
            0#\r\n
            0$\r\n
        */

        long? progressSnap = progress != null ? (reader.BaseStream.Length-reader.BaseStream.Position) / 100 : null;
        var progressC = 0;
        long counter = 0;

        while (!reader.EndOfStream)
        {
            if (cancellationToken is {IsCancellationRequested: true})
            {
                return;
            }

            var c = (char)reader.Read();
            
            if (reader.EndOfStream)
            {
                //Wait for new input from simulator
                await Task.Delay(50);
            }

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
                    {
                        switch (c)
                        {
                            case '0':
                            case '1':
                                if(!addedTime) break;
                                parsingSignalType = VcdLineType.Reg;
                                parsingPos = ParsingPosition.Signal;
                                break;
                            case 'b':
                                if(!addedTime) break;
                                parsingSignalType = VcdLineType.Integer;
                                parsingPos = ParsingPosition.Signal;
                                currentInteger = 0;
                                break;
                            case '#':
                                currentTime = 0L;
                                parsingPos = ParsingPosition.Time;
                                break;
                        }
                    }

                    break;
                
                case ParsingPosition.Signal:
                    if(!addedTime) continue;
                    switch (parsingSignalType)
                    {
                        case VcdLineType.Integer:
                            switch (c)
                            {
                                case '0':
                                    currentInteger <<= 1;
                                    break;
                                case '1':
                                    currentInteger <<= 1;
                                    currentInteger += 1;
                                    break;
                                default:
                                    if (lastC is ' ')
                                    {
                                        signalRegister[c].AddChange(changeTimes.Count-1, currentInteger);
                                        parsingPos = ParsingPosition.None;
                                    }
                                    break;
                            }
                            break;
                        case VcdLineType.Reg:
                            byte data = lastC switch
                            {
                                '0' => 0,
                                '1' => 1,
                                'Z' => 2,
                                'X' => 3,
                                _ => byte.MaxValue,
                            };
                            signalRegister[c].AddChange(changeTimes.Count-1, data);
                            parsingPos = ParsingPosition.None;
                            break;
                    }
                    break;
                
                case ParsingPosition.Time:
                    switch (c)
                    {
                        case '\r':
                        case '\n':
                            changeTimes.Add(currentTime);
                            addedTime = true;
                            parsingPos = ParsingPosition.None;
                            break;
                        default:
                            currentTime = AddNumber(currentTime, c);
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
        if (c is >= '0' and <= '9')
        {
            return n * 10 + (c - '0');
        }
        throw new FormatException("Invalid time parsing");
    }
}