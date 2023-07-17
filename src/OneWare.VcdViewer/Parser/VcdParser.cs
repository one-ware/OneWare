using System.Text;
using DynamicData;
using OneWare.Shared.Extensions;

namespace OneWare.VcdViewer.Parser;

public class VcdParser
{
    private const int MaxDefinitionSize = 100000;
    
    public static void ParseVcd(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        var definition = ReadDefinition(reader);
    }

    private static VcdDefinition ReadDefinition(TextReader reader)
    {
        var definition = new VcdDefinition();
        string? keyWord = null;
        string? value = null;
        
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
                                definition.TimeScale = value;
                                break;
                            case "$var":
                                var words = value!.Split(' ');
                                if (words.Length == 4)
                                {
                                    definition.Scopes.Last().Signals.Add(new VcdSignal(words[0], int.Parse(words[1]), words[2], words[3]));
                                }
                                break;
                            case "$scope":
                                definition.Scopes.Add(new VcdScope(value!));
                                break;
                            case "$upscope":
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
            }
            else
            {
                value += x;
            }
            return true;
        });
        return definition;
    }
}
