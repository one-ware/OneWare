using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;

namespace OneWare.Essentials.UnitTests;

/// <summary>
///     Guards the completion text edits the client announces support for in
///     <c>LanguageServiceLsp</c>. The upstream LSP library detected an InsertReplaceEdit by checking
///     for a string "insert" property, which it never is, so such edits silently lost their ranges.
/// </summary>
public class CompletionItemDeserializationTests
{
    private readonly LspSerializer _serializer = new();

    private const string InsertReplaceEditJson = """
        {
          "label": "myHelperFunction",
          "kind": 3,
          "textEdit": {
            "newText": "myHelperFunction",
            "insert": { "start": { "line": 1, "character": 0 }, "end": { "line": 1, "character": 12 } },
            "replace": { "start": { "line": 1, "character": 0 }, "end": { "line": 1, "character": 12 } }
          }
        }
        """;

    private const string TextEditJson = """
        {
          "label": "aa",
          "kind": 12,
          "textEdit": {
            "newText": "aa",
            "range": { "start": { "line": 2, "character": 24 }, "end": { "line": 2, "character": 25 } }
          }
        }
        """;

    [Fact]
    public void InsertReplaceEdit_KeepsItsRanges()
    {
        var item = _serializer.DeserializeObject<CompletionItem>(InsertReplaceEditJson);

        Assert.NotNull(item.TextEdit);
        Assert.True(item.TextEdit!.IsInsertReplaceEdit);
        Assert.False(item.TextEdit.IsTextEdit);

        var edit = item.TextEdit.InsertReplaceEdit!;
        Assert.Equal(1, edit.Insert.Start.Line);
        Assert.Equal(0, edit.Insert.Start.Character);
        Assert.Equal(12, edit.Replace.End.Character);
    }

    [Fact]
    public void TextEdit_KeepsItsRange()
    {
        var item = _serializer.DeserializeObject<CompletionItem>(TextEditJson);

        Assert.NotNull(item.TextEdit);
        Assert.True(item.TextEdit!.IsTextEdit);
        Assert.NotNull(item.TextEdit.TextEdit!.Range);
        Assert.Equal(2, item.TextEdit.TextEdit.Range.Start.Line);
        Assert.Equal(24, item.TextEdit.TextEdit.Range.Start.Character);
        Assert.Equal(25, item.TextEdit.TextEdit.Range.End.Character);
    }
}
