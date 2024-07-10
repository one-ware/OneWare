using System.Collections.Immutable;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.EditorExtensions;

namespace OneWare.Essentials.Helpers;

public static class SemanticTokenHelper
{
    public static List<SemanticToken> ParseSemanticTokens(ImmutableArray<int> data, SemanticTokensLegend legend)
    {
        var tokens = new List<SemanticToken>();
        var line = 0;
        var character = 0;
        
        var types = legend.TokenTypes.ToArray();
        var modifiers = legend.TokenModifiers.ToArray();
        
        for (var i = 0; i < data.Length; i += 5)
        {
            var deltaLine = data[i];
            var deltaStartCharacter = data[i + 1];
            var length = data[i + 2];
            var tokenType = data[i + 3];
            var tokenModifiersBitset = data[i + 4];

            line += deltaLine;
            if (deltaLine == 0)
            {
                character += deltaStartCharacter;
            }
            else
            {
                character = deltaStartCharacter;
            }
            
            if(tokenType < 0 || tokenType >= types!.Length)
                throw new InvalidOperationException("Invalid token type");
            
            var tokenModifiers = new List<SemanticTokenModifier>();
            for (var bit = 0; bit < modifiers.Length; bit++)
            {
                if ((tokenModifiersBitset & (1 << bit)) != 0)
                {
                    tokenModifiers.Add(modifiers[bit]);
                }
            }

            tokens.Add(new SemanticToken
            {
                Line = line,
                StartCharacter = character,
                Length = length,
                TokenType = types![tokenType],
                TokenModifiers = tokenModifiers.ToArray()
            });
        }

        return tokens;
    }
}