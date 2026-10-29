using GitHub.Copilot;
using OneWare.Essentials.Models;

namespace OneWare.Copilot.Services;

// Images returned by tools (MCP screenshots, image files the agent read, …) are shown in the chat.
// Large or repeated images are not inlined in the tool result but persisted once as a session.binary_asset
// event and referenced by id, so those assets are cached until the result that references them arrives.
public abstract partial class CopilotChatServiceBase
{
    /// <summary>Images larger than this are not shown, which keeps the chat history folder small.</summary>
    internal const int MaxChatImageBytes = 20 * 1024 * 1024;

    private const int MaxCachedImageAssets = 16;

    private readonly Lock _imageLock = new();
    private readonly Dictionary<string, SessionBinaryAssetData> _imageAssets = new(StringComparer.Ordinal);
    private readonly Queue<string> _imageAssetOrder = new();
    private readonly Dictionary<string, string> _imageToolNames = new(StringComparer.Ordinal);
    private readonly List<PendingToolImage> _pendingToolImages = [];

    private sealed record PendingToolImage(string AssetId, string? Description, string ToolCallId, string? AgentId,
        string? ToolName);

    internal readonly record struct ToolImage(string Base64, string MimeType, string? Description, string? AssetId);

    private void RememberToolName(string? toolCallId, string? toolName)
    {
        if (string.IsNullOrEmpty(toolCallId) || string.IsNullOrEmpty(toolName)) return;
        lock (_imageLock) _imageToolNames[toolCallId] = toolName;
    }

    private void HandleBinaryAsset(SessionBinaryAssetData asset)
    {
        if (string.IsNullOrEmpty(asset.AssetId) || !IsImage(asset.MimeType) || string.IsNullOrEmpty(asset.Data))
            return;

        List<PendingToolImage> ready;
        lock (_imageLock)
        {
            if (_imageAssets.TryAdd(asset.AssetId, asset))
            {
                _imageAssetOrder.Enqueue(asset.AssetId);
                while (_imageAssetOrder.Count > MaxCachedImageAssets)
                    _imageAssets.Remove(_imageAssetOrder.Dequeue());
            }

            ready = _pendingToolImages.Where(x => x.AssetId == asset.AssetId).ToList();
            _pendingToolImages.RemoveAll(x => x.AssetId == asset.AssetId);
        }

        foreach (var pending in ready)
            EmitToolImage(new ToolImage(asset.Data, asset.MimeType, pending.Description ?? asset.Description,
                asset.AssetId), pending.ToolCallId, pending.AgentId, pending.ToolName);
    }

    private void HandleToolImages(ToolExecutionCompleteData data, string? agentId)
    {
        string? toolName;
        lock (_imageLock)
            _imageToolNames.Remove(data.ToolCallId ?? string.Empty, out toolName);

        if (data.Result == null) return;

        var images = ExtractToolImages(data.Result, ResolveImageAsset, out var missingAssets);
        foreach (var image in images)
            EmitToolImage(image, data.ToolCallId, agentId, toolName);

        if (missingAssets.Count == 0) return;

        // The asset event may follow the result; show the image when it arrives.
        lock (_imageLock)
        {
            foreach (var reference in missingAssets)
                _pendingToolImages.Add(new PendingToolImage(reference.AssetId, reference.Description,
                    data.ToolCallId ?? string.Empty, agentId, toolName));
            while (_pendingToolImages.Count > MaxCachedImageAssets)
                _pendingToolImages.RemoveAt(0);
        }
    }

    private SessionBinaryAssetData? ResolveImageAsset(string assetId)
    {
        lock (_imageLock)
            return _imageAssets.GetValueOrDefault(assetId);
    }

    private void EmitToolImage(ToolImage image, string? toolCallId, string? agentId, string? toolName = null)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(image.Base64);
        }
        catch (FormatException)
        {
            return;
        }

        if (bytes.Length == 0 || bytes.Length > MaxChatImageBytes) return;

        EventReceived?.Invoke(this, new ChatImageEvent(bytes, image.MimeType)
        {
            AgentId = agentId,
            Description = string.IsNullOrWhiteSpace(image.Description) ? null : image.Description,
            ToolCallId = toolCallId,
            ToolName = toolName
        });
    }

    private void ResetImageState()
    {
        lock (_imageLock)
        {
            _imageAssets.Clear();
            _imageAssetOrder.Clear();
            _imageToolNames.Clear();
            _pendingToolImages.Clear();
        }
    }

    /// <summary>
    /// The images of a tool result. The structured contents and the model-facing binary results usually carry the
    /// same image, so each image is returned once.
    /// </summary>
    internal static List<ToolImage> ExtractToolImages(ToolExecutionCompleteResult result,
        Func<string, SessionBinaryAssetData?> resolveAsset, out List<BinaryAssetReference> missingAssets)
    {
        var images = new List<ToolImage>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        missingAssets = [];

        void Add(string? data, string? mimeType, string? description, string? assetId = null)
        {
            if (string.IsNullOrEmpty(data) || !IsImage(mimeType) || !seen.Add(data)) return;
            images.Add(new ToolImage(data, mimeType!, description, assetId));
        }

        foreach (var content in result.Contents ?? [])
            if (content is ToolExecutionCompleteContentImage image)
                Add(image.Data, image.MimeType, null);

        var imageBinaries = 0;
        foreach (var binary in result.BinaryResultsForLlm ?? [])
        {
            if (IsImage(binary.PersistedBinaryImage?.MimeType ?? binary.BinaryAssetReference?.MimeType))
                imageBinaries++;

            if (binary.PersistedBinaryImage is { } inline)
            {
                Add(inline.Data, inline.MimeType, inline.Description);
            }
            else if (binary.BinaryAssetReference is { } reference && IsImage(reference.MimeType) &&
                     !string.IsNullOrEmpty(reference.AssetId))
            {
                if (resolveAsset(reference.AssetId) is { } asset)
                    Add(asset.Data, asset.MimeType ?? reference.MimeType,
                        reference.Description ?? asset.Description, reference.AssetId);
                else if (missingAssets.All(x => x.AssetId != reference.AssetId))
                    missingAssets.Add(reference);
            }
        }

        // The contents already carried every image, so the referenced assets are duplicates.
        if (images.Count >= imageBinaries) missingAssets.Clear();

        return images;
    }

    internal static bool IsImage(string? mimeType) =>
        mimeType != null && mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
}
