#pragma warning disable GHCP001 // Binary tool results are experimental in the SDK.
using System.Collections.Generic;
using System.Linq;
using GitHub.Copilot;
using OneWare.Copilot.Services;
using Xunit;

namespace OneWare.Copilot.UnitTests;

public sealed class CopilotToolImageTests
{
    private const string Png = "iVBORw0KGgo=";
    private const string Jpeg = "/9j/4AAQ";

    private static SessionBinaryAssetData? NoAssets(string _) => null;

    [Fact]
    public void ExtractToolImages_ReturnsEachImageOnce()
    {
        var result = new ToolExecutionCompleteResult
        {
            Content = string.Empty,
            Contents =
            [
                new ToolExecutionCompleteContentImage { Data = Png, MimeType = "image/png" },
                new ToolExecutionCompleteContentImage { Data = "AAAA", MimeType = "audio/wav" }
            ],
            BinaryResultsForLlm =
            [
                new PersistedBinaryResult(new PersistedBinaryImage
                    {
                        Type = PersistedBinaryImageType.Image, Data = Png, MimeType = "image/png", Description = "Screenshot" }),
                new PersistedBinaryResult(new PersistedBinaryImage
                    {
                        Type = PersistedBinaryImageType.Image, Data = Jpeg, MimeType = "image/jpeg", Description = "Photo" })
            ]
        };

        var images = CopilotChatServiceBase.ExtractToolImages(result, NoAssets, out var missing);

        Assert.Equal([Png, Jpeg], images.Select(x => x.Base64));
        Assert.Equal("Photo", images[1].Description);
        Assert.Empty(missing);
    }

    [Fact]
    public void ExtractToolImages_ResolvesAssetReferences()
    {
        var asset = new SessionBinaryAssetData { Type = BinaryAssetType.Image, ByteLength = 8, AssetId = "sha256:1", Data = Png, MimeType = "image/png" };
        var result = new ToolExecutionCompleteResult
        {
            Content = string.Empty,
            BinaryResultsForLlm =
            [
                new PersistedBinaryResult(new BinaryAssetReference
                    {
                        Type = BinaryAssetReferenceType.Image, ByteLength = 8, AssetId = "sha256:1", MimeType = "image/png", Description = "Generated" }),
                new PersistedBinaryResult(new BinaryAssetReference { Type = BinaryAssetReferenceType.Image, ByteLength = 8, AssetId = "sha256:2", MimeType = "image/png" })
            ]
        };

        var images = CopilotChatServiceBase.ExtractToolImages(result,
            id => id == asset.AssetId ? asset : null, out var missing);

        var image = Assert.Single(images);
        Assert.Equal(Png, image.Base64);
        Assert.Equal("Generated", image.Description);
        Assert.Equal("sha256:2", Assert.Single(missing).AssetId);
    }

    [Fact]
    public void ExtractToolImages_DoesNotWaitForAssetsTheContentsAlreadyCarried()
    {
        var result = new ToolExecutionCompleteResult
        {
            Content = string.Empty,
            Contents = [new ToolExecutionCompleteContentImage { Data = Png, MimeType = "image/png" }],
            BinaryResultsForLlm =
                [new PersistedBinaryResult(new BinaryAssetReference { Type = BinaryAssetReferenceType.Image, ByteLength = 8, AssetId = "sha256:1", MimeType = "image/png" })]
        };

        var images = CopilotChatServiceBase.ExtractToolImages(result, NoAssets, out var missing);

        Assert.Single(images);
        Assert.Empty(missing);
    }

    [Fact]
    public void ExtractToolImages_IgnoresResultsWithoutImages()
    {
        var result = new ToolExecutionCompleteResult
        {
            Content = "text",
            BinaryResultsForLlm =
            [
                new PersistedBinaryResult(new OmittedBinaryResult { Type = OmittedBinaryType.Image, OmittedReason = OmittedBinaryOmittedReason.TooLarge, MimeType = "image/png", ByteLength = 1 << 30 }),
                new PersistedBinaryResult(new PersistedBinaryImage { Type = PersistedBinaryImageType.Resource, Data = "JVBE", MimeType = "application/pdf" })
            ]
        };

        Assert.Empty(CopilotChatServiceBase.ExtractToolImages(result, NoAssets, out var missing));
        Assert.Empty(missing);
    }
}
