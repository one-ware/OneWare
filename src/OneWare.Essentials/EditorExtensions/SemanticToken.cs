using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Essentials.EditorExtensions;

public struct SemanticToken
{
    public int Line { get; init; }
    public int StartCharacter { get; init; }
    public int Length { get; init; }
    public SemanticTokenType TokenType { get; init; }
    public SemanticTokenModifier[] TokenModifiers { get; init; }
}