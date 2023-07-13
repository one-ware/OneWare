using System.Text.Json;
using AvaloniaEdit.Document;
using OneWare.Shared.EditorExtensions;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Json.Formatting;

public class JsonFormatter : IFormattingStrategy
{
    public void Format(TextDocument document)
    {
        var options = new JsonSerializerOptions(){
            WriteIndented = true
        };

        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(document.Text);
            var format = JsonSerializer.Serialize(jsonElement, options);

            document.Text = format;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
        
    }
}