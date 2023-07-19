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
        using var reader = new StreamReader(stream);

        var definition = ReadDefinition(reader);

        return definition;
    }

    private static VcdDefinition ReadDefinition(TextReader reader)
    {
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
}
