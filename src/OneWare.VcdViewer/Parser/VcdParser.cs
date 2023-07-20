using System.Text;
using DynamicData;
using OneWare.Shared.Extensions;
using OneWare.VcdViewer.Models;
using OneWare.WaveFormViewer.Enums;
using OneWare.WaveFormViewer.Models;

namespace OneWare.VcdViewer.Parser;

public static class VcdParser
{
    private const int MaxDefinitionSize = 100000;
    private const int BufferSize = 1024;

    public static (VcdFile, StreamReader) ParseVcdDefinition(string path)
    {
        var stream = File.OpenRead(path);
        var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);
        
        var definition = ReadDefinition(reader);
        
        var vcdFile = new VcdFile(definition);
        
        return (vcdFile, reader);
    }

    public static async Task StartAndReportProgressAsync(StreamReader reader, VcdFile vcdFile, IProgress<int> progress)
    {
        await Task.Run(() =>
        {
            ReadSignals(reader, vcdFile, progress);
        });
        reader.Dispose();
    }
    
    public static Task<VcdFile> ParseVcdAsync(string path)
    {
        return Task.Run(() => ParseVcd(path));
    }
    
    public static VcdFile ParseVcd(string path)
    {
        using var stream = File.OpenRead(path);
        return ParseVcd(stream);
    }
    
    public static VcdFile ParseVcd(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, BufferSize);
        
        var definition = ReadDefinition(reader);
        
        var vcdFile = new VcdFile(definition);

        ReadSignals(reader, vcdFile);
        
        return vcdFile;
    }

    private static VcdDefinition ReadDefinition(TextReader reader)
    {
        var definition = new VcdDefinition();
        IScopeHolder currentScope = definition;
        string? keyWord = null;
        var words = new List<string>();
        
        reader.ProcessWords(MaxDefinitionSize, x =>
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
                                    var newSignal = new VcdSignal(Enum.Parse<SignalLineType>(words[0], true), int.Parse(words[1]), words[2][0], words[3]);
                                    currentScope.Signals.Add(newSignal);
                                    definition.SignalRegister.TryAdd(newSignal.Id, newSignal);
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
        });
        
        return definition;
    }

    private enum ParsingPosition
    {
        None,
        Time,
        Signal
    }

    private static void ReadSignals(StreamReader reader, VcdFile file, IProgress<int>? progress = null)
    {
        var currentTime = 0L;
        var currentInteger = 0;
        
        var lastC = ' ';
        var parsingPos = ParsingPosition.None;
        var parsingSignalType = SignalLineType.Reg;

        /*  Example Block
            #78083021000\r\n
            0#\r\n
            0$\r\n
        */
        
        long? progressSnap = progress != null ? reader.BaseStream.Length / 100 : null;
        var progressC = 0;
        long counter = 0;
        
        while(!reader.EndOfStream)
        {
            var c = (char)reader.Read();
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

            switch (c)
            {
                case '\r':
                    break;
                case '\n':
                    switch (parsingPos)
                    {
                        case ParsingPosition.Time:
                            file.LastChangeTime = currentTime;
                            break;
                    }
                    //stringBuilder.Clear();

                    parsingPos = ParsingPosition.None;
                    break;
                case '#' when lastC is '\n':
                    //stringBuilder.Clear();
                    currentTime = 0L;
                    parsingPos = ParsingPosition.Time;
                    break;
                case '0' when lastC is '\n' && parsingPos is ParsingPosition.None:
                case '1' when lastC is '\n' && parsingPos is ParsingPosition.None:
                    parsingSignalType = SignalLineType.Reg;
                    parsingPos = ParsingPosition.Signal;
                    break;
                case 'b' when lastC is '\n' && parsingPos is ParsingPosition.None:
                    parsingSignalType = SignalLineType.Integer;
                    parsingPos = ParsingPosition.Signal;
                    currentInteger = 0;
                    break;
                case '0' when parsingPos is ParsingPosition.Signal && parsingSignalType is SignalLineType.Integer:
                    currentInteger <<= 1;
                    break;
                case '1' when parsingPos is ParsingPosition.Signal && parsingSignalType is SignalLineType.Integer:
                    currentInteger <<= 1;
                    currentInteger += 1;
                    break;
                default:
                    switch (parsingPos)
                    {
                        case ParsingPosition.Signal when parsingSignalType is SignalLineType.Reg:
                            file.Definition.SignalRegister[c].Changes
                                .Add(new WavePart(currentTime, lastC == '1'));
                            break;
                        case ParsingPosition.Signal when parsingSignalType is SignalLineType.Integer && lastC is ' ':
                        {
                            file.Definition.SignalRegister[c].Changes
                                .Add(new WavePart(currentTime, currentInteger));
                            break;
                        }
                        case ParsingPosition.Time:
                            currentTime = AddNumber(currentTime, c);
                            break;
                        default:
                            break;
                    }
                    break;
            }
            lastC = c;
        }

        foreach (var (_, signal) in file.Definition.SignalRegister)
        {
            signal.Changes.Add(new WavePart(long.MaxValue-10, signal.Changes.Last().Data));
        }
    }

    static long AddNumber(long n, char c)
    {
        if (c is >= '0' and <= '9')
        {
            return n * 10 + (c - '0');
        }
        throw new FormatException("Invalid time parsing");
    }
    
    static long ParseLong(StringBuilder stringBuilder)
    {
        long result = 0;
        for (var i = 0; i < stringBuilder.Length; i++)
        {
            var c = stringBuilder[i];
            if (c is >= '0' and <= '9')
            {
                result = result * 10 + (c - '0');
            }
            else
            {
                throw new FormatException("Invalid time parsing");
            }
        }
        return result;
    }
}
