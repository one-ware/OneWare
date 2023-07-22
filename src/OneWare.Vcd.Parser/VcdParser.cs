using System.Text;
using OneWare.Vcd.Parser.Data;
using OneWare.Vcd.Parser.Extensions;

namespace OneWare.Vcd.Parser;

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
        await Task.Run(() => { ReadSignals(reader, vcdFile, progress); });
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
        var parsingSignalType = VcdLineType.Reg;

        /*  Example Block
            #78083021000\r\n
            0#\r\n
            0$\r\n
        */

        long? progressSnap = progress != null ? reader.BaseStream.Length / 100 : null;
        var progressC = 0;
        long counter = 0;

        while (!reader.EndOfStream)
        {
            var c = (char)reader.Read();
            
            if(c is '\r') continue;
            
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
                                parsingSignalType = VcdLineType.Reg;
                                parsingPos = ParsingPosition.Signal;
                                break;
                            case 'b':
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
                                        file.Definition.SignalRegister[c].AddChange(file.Definition.ChangeTimes.Count-1, currentInteger);
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
                            file.Definition.SignalRegister[c].AddChange(file.Definition.ChangeTimes.Count-1, data);
                            parsingPos = ParsingPosition.None;
                            break;
                    }
                    break;
                
                case ParsingPosition.Time:
                    switch (c)
                    {
                        case '\n':
                            file.Definition.ChangeTimes.Add(currentTime);
                            parsingPos = ParsingPosition.None;
                            break;
                        default:
                            currentTime = AddNumber(currentTime, c);
                            break;
                    }
                    break;
            }

            lastC = c;
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
}