using Avalonia.Platform;
using AvaloniaEdit.Document;
using Jint;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Vhdl.Formatting;

public class VhdlFormatter : IFormattingStrategy
{
    public void Format(TextDocument document)
    {
        var settingsService = ContainerLocator.Container.Resolve<ISettingsService>();
        var useSpaces = settingsService.GetSettingValue<bool>("Editor_UseSpaces");
        var indentationSize = settingsService.GetSettingValue<int>("Editor_IndentationSize");
        var indentString = useSpaces ? new string(' ', indentationSize) : "\t";
        var result = Format(document.Text, indentString);
        if (result != null)
            document.Text = result;
    }

    private static string? Format(string source, string indentString)
    {
        try
        {
            using var engine = new Engine();

            using var stream = AssetLoader.Open(new Uri("avares://OneWare.Vhdl/Assets/formatter.js"));
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();

            engine.Execute("const exports = {}");
            engine.Execute(script);

            var settings = new BeautifierSettings
            {
                KeywordCase = "UPPERCASE",
                TypeNameCase = "UPPERCASE",
                EndOfLine = "\n",
                Indentation = indentString,
                AddNewLine = true
                // NewLineSettings = new NewLineSettings
                // {
                //     newLineAfter = new []
                //     {
                //         ";",
                //         "then"
                //     },
                //     noLineAfter = Array.Empty<string>()
                //}
            };

            engine.SetValue("settings", settings);
            engine.SetValue("source", source);
            engine.Execute("var formatted = beautify(source, settings)");
            var result = engine.GetValue("formatted").ToObject() as string;
            return result;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
        }
    }
}

public class BeautifierSettings
{
    public string? KeywordCase { get; set; }

    public string? TypeNameCase { get; set; }

    public string? EndOfLine { get; set; }

    public string? Indentation { get; set; }

    public bool AddNewLine { get; set; }

    public NewLineSettings? NewLineSettings { get; set; }
}

public class NewLineSettings
{
    // ReSharper disable once InconsistentNaming
    public string[]? newLineAfter { get; set; }

    // ReSharper disable once InconsistentNaming
    public string[]? noLineAfter { get; set; }
}