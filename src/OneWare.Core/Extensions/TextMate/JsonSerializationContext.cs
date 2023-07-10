using System.Text.Json.Serialization;
using TextMateSharp.Grammars;

namespace OneWare.Core.Extensions.TextMate;

[JsonSerializable(typeof(GrammarDefinition))]
[JsonSerializable(typeof(LanguageSnippets))]
[JsonSerializable(typeof(LanguageSnippet))]
[JsonSerializable(typeof(LanguageConfiguration))]
[JsonSerializable(typeof(EnterRule))]
[JsonSerializable(typeof(AutoPair))]
[JsonSerializable(typeof(IList<string>))]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}