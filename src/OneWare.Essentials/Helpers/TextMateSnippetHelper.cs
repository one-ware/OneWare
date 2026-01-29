using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform;
using Microsoft.Extensions.Logging;
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
    public static List<TextMateSnippet> ParseVsCodeSnippets(string avaloniaResource)
    {
        var completionItems = new List<TextMateSnippet>();

        try
        {
            using var s = AssetLoader.Open(new Uri(avaloniaResource));

            var snippetsDict = JsonSerializer.Deserialize<Dictionary<string, Snippet>>(s);

            if (snippetsDict == null)
                return completionItems;

            foreach (var snippet in from kvp in snippetsDict let label = kvp.Key select kvp.Value)
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
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return completionItems;
        }

        return completionItems;
    }

    [Serializable]
    private class Snippet
    {
        [JsonPropertyName("prefix")]
        [JsonConverter(typeof(PrefixConverter))]
        public string[]? Prefixes { get; init; }

        [JsonPropertyName("body")] public List<string>? Body { get; init; }

        [JsonPropertyName("description")] public string? Description { get; init; }
    }
}

public class PrefixConverter : JsonConverter<string[]?>
{
    public override string[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var prefix = reader.GetString();
            if (prefix == null) return null;
            return [prefix];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonDocument.ParseValue(ref reader).RootElement.Deserialize<string[]>();

        throw new JsonException("Unexpected token type");
    }

    public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Serialization is not implemented");
    }
}