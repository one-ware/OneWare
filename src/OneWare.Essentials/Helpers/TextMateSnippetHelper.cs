using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Helpers;

public class TextMateSnippet(string label, string content, string? description)
{
    public string Label { get; } = label;
    public string Content { get; } = content;
    public string? Description { get; } = description;
}

public class TextMateSnippetHelper
{
    private readonly ILogger _logger;

    public TextMateSnippetHelper(ILogger logger)
    {
        _logger = logger;
    }

    public List<TextMateSnippet> ParseVsCodeSnippets(string avaloniaResource)
    {
        var completionItems = new List<TextMateSnippet>();

        try
        {
            using var stream = AssetLoader.Open(new Uri(avaloniaResource));
            var snippetsDict = JsonSerializer.Deserialize<Dictionary<string, Snippet>>(stream);

            if (snippetsDict == null)
                return completionItems;

            foreach (var (label, snippet) in snippetsDict)
            {
                if (snippet.Body == null || snippet.Prefixes == null) continue;

                var content = string.Join(Environment.NewLine, snippet.Body);

                foreach (var prefix in snippet.Prefixes)
                {
                    var completionItem = new TextMateSnippet(prefix, content, snippet.Description);
                    completionItems.Add(completionItem);
                }
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }

        return completionItems;
    }

    [Serializable]
    private class Snippet
    {
        [JsonPropertyName("prefix")]
        [JsonConverter(typeof(PrefixConverter))]
        public string[]? Prefixes { get; init; }

        [JsonPropertyName("body")]
        public List<string>? Body { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }
    }
}

public class PrefixConverter : JsonConverter<string[]?>
{
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var prefix = reader.GetString();
            return prefix == null ? null : [prefix];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return JsonDocument.ParseValue(ref reader).RootElement.Deserialize<string[]>();
        }

        throw new JsonException("Unexpected token type for prefix");
    }

    public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Serialization is not implemented");
    }
}
