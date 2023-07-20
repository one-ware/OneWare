using System.Text;
using DynamicData;
using OneWare.Shared.Extensions;
using OneWare.VcdViewer.Models;

namespace OneWare.VcdViewer.Parser;

public class VcdParser
{
    private const int MaxDefinitionSize = 100000;
    
    public static VcdDefinition ParseVcd(string path)
    {
        using var stream = File.OpenRead(path);
        return ParseVcd(stream);
    }
    
    public static VcdDefinition ParseVcd(Stream stream, float accuracy = 1f)
    {
        var definition = ReadDefinition(stream);

        var endDefinition = stream.Position;
        
        ReadSignals(stream, definition, stream.Position, stream.Length, accuracy);
        
        return definition;
    }

    private static VcdDefinition ReadDefinition(Stream stream)
    {
        using var reader = new StreamReader(stream, null, true, -1, true);
        
        var definition = new VcdDefinition();
        IScopeHolder currentScope = definition;
        string? keyWord = null;
        var words = new List<string>();
        
        reader.ProcessWords(MaxDefinitionSize, x =>
        {
            if (x.StartsWith('$'))
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
                                    currentScope.Signals.Add(new VcdSignal(words[0], int.Parse(words[1]), words[2], words[3]));
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
                    case "$enddefinitions":
                        return false;
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

    private enum ParsingContext
    {
        None,
        Time,
        Signal
    }
    
    private static void ReadSignals(Stream stream, VcdDefinition definition, long start, long end, float accuracy)
    {        
        stream.Seek(start, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, null, true, -1, true);

        var stack = new Stack<(long, List<string>)>();
        
        var gap =  (1f / accuracy)-1;
        var lastC = ' ';
        var parsingPos = ParsingContext.None;
        var stringBuilder = new StringBuilder();

        var lastBlockLength = 0;

        var test = "";
        
        while(stream.Position < end)
        {
            lastBlockLength++;
            var c = (char)reader.Read();

            test += c;
            
            switch (c)
            {
                case '\r':
                    break;
                case ' ':
                case '\n':
                    c = ' ';
                    switch (parsingPos)
                    {
                        case ParsingContext.Time:
                            stack.Push((ParseLong(stringBuilder), new List<string>()));
                            stringBuilder.Clear();

                            test = "";
                            
                            //Block end
                            stream.Seek((long)(lastBlockLength*gap), SeekOrigin.Current);
                            reader.DiscardBufferedData();
                            lastBlockLength = 0;

                            break;
                    }
                    parsingPos = ParsingContext.None;
                    break;
                case '#' when lastC is ' ':
                    stringBuilder.Clear();
                    parsingPos = ParsingContext.Time;
                    break;
                default:
                    stringBuilder.Append(c);
                    break;
            }
            lastC = c;
        }
        
        Console.WriteLine(stack.Count);
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
