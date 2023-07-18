using System.Text;
using DynamicData;
using OneWare.Shared.Extensions;

namespace OneWare.VcdViewer.Parser;

public class VcdParser
{
    private const int MaxDefinitionSize = 100000;
    
    public static VcdDefinition ParseVcd(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);

        var definition = ReadDefinition(reader);

        return definition;
    }

    private static VcdDefinition ReadDefinition(TextReader reader)
    {
        var definition = new VcdDefinition();
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
                                    definition.Scopes.Last().Signals.Add(new VcdSignal(words[0], int.Parse(words[1]), words[2], words[3]));
                                }
                                break;
                            case "$scope":
                                definition.Scopes.Add(new VcdScope(string.Join(' ', words)));
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
}
