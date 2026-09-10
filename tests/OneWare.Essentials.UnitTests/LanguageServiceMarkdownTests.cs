using System.Collections.Generic;
using System.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class LanguageServiceMarkdownTests
{
    private static SignatureInformation Signature(string label, params ParameterInformation[] parameters)
    {
        return new SignatureInformation
        {
            Label = label,
            Parameters = new Container<ParameterInformation>(parameters)
        };
    }

    [Fact]
    public void EscapeMarkdown_KeepsEmphasisCharactersVisible()
    {
        var escaped = StringOrMarkupContentExtensions.EscapeMarkdown("value_of_x is *not* 2 ~ 3");

        Assert.Equal(@"value\_of\_x is \*not\* 2 \~ 3", escaped);
    }

    [Fact]
    public void EscapeMarkdown_KeepsLineBreaks()
    {
        var escaped = StringOrMarkupContentExtensions.EscapeMarkdown("first\r\nsecond");

        //Two trailing spaces are what makes the renderer break the line
        Assert.Equal("first  \nsecond", escaped);
    }

    [Fact]
    public void GetMarkdown_PassesServerMarkdownThrough()
    {
        var content = new StringOrMarkupContent(new MarkupContent
        {
            Kind = MarkupKind.Markdown,
            Value = "```ts\nconst a = 1;\n```\nMakes it **bold**.\n"
        });

        Assert.Equal("```ts\nconst a = 1;\n```\nMakes it **bold**.", content.GetMarkdown());
    }

    [Fact]
    public void GetMarkdown_EscapesPlainTextDocumentation()
    {
        var content = new StringOrMarkupContent(new MarkupContent
        {
            Kind = MarkupKind.PlainText,
            Value = "returns snake_case"
        });

        Assert.Equal(@"returns snake\_case", content.GetMarkdown());
    }

    [Fact]
    public void GetMarkdown_ReturnsNullForEmptyDocumentation()
    {
        Assert.Null(new StringOrMarkupContent("   ").GetMarkdown());
        Assert.Null(((StringOrMarkupContent?)null).GetMarkdown());
    }

    [Fact]
    public void BuildHoverText_UsesFencesForMarkedStringsWithLanguage()
    {
        var hover = new Hover
        {
            Contents = new MarkedStringsOrMarkupContent(
                new MarkedString("typescript", "function add(a: number): number"),
                new MarkedString("Adds a number."))
        };

        Assert.Equal("```typescript\nfunction add(a: number): number\n```\n\nAdds a number.",
            LanguageServiceMarkdown.BuildHoverText(hover));
    }

    [Fact]
    public void FormatDiagnostics_ShowsSeverityAndOrigin()
    {
        var errors = new List<ErrorListItem>
        {
            new("Type 'string' is not assignable.", ErrorType.Error, "/a.ts", "typescript", 1, 1, 1, 5, "2322"),
            new("Value is never read.", ErrorType.Hint, "/a.ts", null, 1, 1, 1, 5)
        };

        Assert.Equal("**Error** `typescript 2322`  \nType 'string' is not assignable.\n\n" +
                     "**Hint**  \nValue is never read.",
            LanguageServiceMarkdown.FormatDiagnostics(errors));
    }

    [Fact]
    public void FormatDiagnostics_ReturnsNullWithoutDiagnostics()
    {
        Assert.Null(LanguageServiceMarkdown.FormatDiagnostics([]));
    }

    [Fact]
    public void FormatSignatureLabel_HighlightsActiveParameterGivenAsText()
    {
        var signature = Signature("add(a: number, b: number): number",
            new ParameterInformation { Label = "a: number" },
            new ParameterInformation { Label = "b: number" });

        var formatted = LanguageServiceMarkdown.FormatSignatureLabel(signature,
            signature.Parameters!.ElementAt(1));

        Assert.Equal("add(a: number, **b: number**): number", formatted);
    }

    [Fact]
    public void FormatSignatureLabel_HighlightsActiveParameterGivenAsOffsets()
    {
        var signature = Signature("add(a: number, b: number): number",
            new ParameterInformation { Label = (4, 13) },
            new ParameterInformation { Label = (15, 24) });

        var formatted = LanguageServiceMarkdown.FormatSignatureLabel(signature,
            signature.Parameters!.ElementAt(0));

        Assert.Equal("add(**a: number**, b: number): number", formatted);
    }

    [Fact]
    public void FormatSignatureLabel_EscapesLabelWithoutActiveParameter()
    {
        var signature = Signature("read_all(path_name: string): string");

        Assert.Equal(@"read\_all(path\_name: string): string",
            LanguageServiceMarkdown.FormatSignatureLabel(signature, null));
    }

    [Fact]
    public void FormatSignatureLabel_IgnoresOffsetsOutsideTheLabel()
    {
        var signature = Signature("add(a)", new ParameterInformation { Label = (4, 99) });

        Assert.Equal("add(a)", LanguageServiceMarkdown.FormatSignatureLabel(signature,
            signature.Parameters!.ElementAt(0)));
    }

    [Fact]
    public void GetActiveParameter_FallsBackToTheRequestWideIndex()
    {
        var signature = Signature("add(a: number, b: number)",
            new ParameterInformation { Label = "a: number" },
            new ParameterInformation { Label = "b: number" });

        //Most servers only report the active parameter once for the whole request
        var signatureHelp = new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature),
            ActiveParameter = 1
        };

        Assert.Equal("b: number",
            LanguageServiceMarkdown.GetActiveParameter(signatureHelp, signature)?.Label.Label);
    }

    [Fact]
    public void GetActiveParameter_PrefersTheIndexOfTheSignature()
    {
        var signature = new SignatureInformation
        {
            Label = "add(a, b)",
            ActiveParameter = 0,
            Parameters = new Container<ParameterInformation>(
                new ParameterInformation { Label = "a" },
                new ParameterInformation { Label = "b" })
        };

        var signatureHelp = new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature),
            ActiveParameter = 1
        };

        Assert.Equal("a", LanguageServiceMarkdown.GetActiveParameter(signatureHelp, signature)?.Label.Label);
    }

    [Fact]
    public void GetActiveParameter_ReturnsNullForAnIndexOutOfRange()
    {
        var signature = Signature("add(a)", new ParameterInformation { Label = "a" });
        var signatureHelp = new SignatureHelp
        {
            Signatures = new Container<SignatureInformation>(signature),
            ActiveParameter = 5
        };

        Assert.Null(LanguageServiceMarkdown.GetActiveParameter(signatureHelp, signature));
    }

    [Fact]
    public void FormatSignatureDocumentation_LeadsWithTheActiveParameter()
    {
        var signature = new SignatureInformation
        {
            Label = "add(a: number)",
            Documentation = new StringOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown, Value = "Adds two numbers."
            }),
            Parameters = new Container<ParameterInformation>(new ParameterInformation
            {
                Label = (4, 13),
                Documentation = new StringOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown, Value = "The first number."
                })
            })
        };

        Assert.Equal("**a: number** — The first number.\n\nAdds two numbers.",
            LanguageServiceMarkdown.FormatSignatureDocumentation(signature, signature.Parameters!.ElementAt(0)));
    }

    [Fact]
    public void FormatSignatureDocumentation_ReturnsNullWhenNothingIsDocumented()
    {
        Assert.Null(LanguageServiceMarkdown.FormatSignatureDocumentation(Signature("add()"), null));
    }

    [Fact]
    public void BuildCompletionDescription_PutsTheDetailIntoACodeBlock()
    {
        var item = new CompletionItem
        {
            Label = "volume",
            Detail = "(property) volume: number",
            Documentation = new StringOrMarkupContent(new MarkupContent
            {
                Kind = MarkupKind.Markdown, Value = "How **loud** it is."
            })
        };

        Assert.Equal("```typescript\n(property) volume: number\n```\n\nHow **loud** it is.",
            LanguageServiceMarkdown.BuildCompletionDescription(item, "typescript"));
    }

    [Fact]
    public void BuildCompletionDescription_MarksDeprecatedItems()
    {
        var item = new CompletionItem
        {
            Label = "old",
            Detail = "function old(): void",
            Tags = new Container<CompletionItemTag>(CompletionItemTag.Deprecated)
        };

        Assert.Equal("**Deprecated**\n\n```typescript\nfunction old(): void\n```",
            LanguageServiceMarkdown.BuildCompletionDescription(item, "typescript"));
    }

    [Fact]
    public void BuildCompletionDescription_StaysEmptyWhenTheItemAddsNothing()
    {
        //Nothing but the label the completion list already shows, so no tip should open
        Assert.Null(LanguageServiceMarkdown.BuildCompletionDescription(new CompletionItem { Label = "volume" },
            "typescript"));
    }

    [Fact]
    public void BuildCompletionDescription_UsesLabelDetails()
    {
        var item = new CompletionItem
        {
            Label = "map",
            LabelDetails = new CompletionItemLabelDetails
            {
                Detail = "(f: fn(T) -> U)",
                Description = "core::iter"
            }
        };

        Assert.Equal("```rust\nmap(f: fn(T) -> U)\n```\n\ncore::iter",
            LanguageServiceMarkdown.BuildCompletionDescription(item, "rust"));
    }
}
