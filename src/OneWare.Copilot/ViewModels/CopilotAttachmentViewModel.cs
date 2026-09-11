using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHub.Copilot;

namespace OneWare.Copilot.ViewModels;

/// <summary>
/// A single attachment chip shown in the Copilot chat attachment strip. Represents either a whole
/// file, a code selection within a file, or an inline image blob (e.g. pasted from clipboard), and
/// knows how to convert itself into an SDK <see cref="Attachment"/> when a message is sent.
/// </summary>
public sealed class CopilotAttachmentViewModel : ObservableObject, IDisposable
{
    private readonly string? _selectionText;
    private readonly SelectionRange? _selection;
    private readonly byte[]? _imageData;
    private readonly string? _mimeType;

    public string FilePath { get; }

    public string DisplayName { get; }

    /// <summary>Short line-range hint shown on the chip, e.g. "L10-24". Null for whole-file attachments.</summary>
    public string? Detail { get; }

    /// <summary>True for the implicit, auto-tracked focused-file chip.</summary>
    public bool IsActiveFile { get; }

    public string IconResourceKey { get; }

    /// <summary>True when this attachment wraps inline image data rather than a file path.</summary>
    public bool IsImage { get; }

    /// <summary>Small decoded thumbnail for image attachments; null for file/selection attachments.</summary>
    public Bitmap? ThumbnailBitmap { get; }

    public IRelayCommand RemoveCommand { get; }

    public CopilotAttachmentViewModel(
        string filePath,
        string displayName,
        bool isActiveFile,
        Action<CopilotAttachmentViewModel> onRemove,
        SelectionRange? selection = null,
        string? selectionText = null,
        string iconResourceKey = "VsImageLib.File16X")
    {
        FilePath = filePath;
        DisplayName = displayName;
        IsActiveFile = isActiveFile;
        IconResourceKey = iconResourceKey;
        _selection = selection;
        _selectionText = selectionText;

        if (selection is { } s)
        {
            Detail = s.StartLine == s.EndLine ? $"{s.StartLine}" : $"{s.StartLine}-{s.EndLine}";
        }

        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    /// <summary>Creates an image-blob attachment from raw bytes (e.g. a clipboard paste).</summary>
    public CopilotAttachmentViewModel(
        byte[] imageData,
        string mimeType,
        string displayName,
        bool isActiveFile,
        Action<CopilotAttachmentViewModel> onRemove)
    {
        FilePath = string.Empty;
        DisplayName = displayName;
        IsActiveFile = isActiveFile;
        IsImage = true;
        IconResourceKey = "VsImageLib.File16X";
        _imageData = imageData;
        _mimeType = mimeType;

        try
        {
            using var ms = new MemoryStream(imageData);
            ThumbnailBitmap = Bitmap.DecodeToWidth(ms, 24);
        }
        catch
        {
            // Best-effort thumbnail; show nothing if decoding fails.
        }

        RemoveCommand = new RelayCommand(() => onRemove(this));
    }

    public Attachment ToSdkAttachment()
    {
        if (IsImage && _imageData != null && _mimeType != null)
        {
            return new AttachmentBlob
            {
                Data = Convert.ToBase64String(_imageData),
                MimeType = _mimeType,
                DisplayName = DisplayName
            };
        }

        if (_selection is { } s && _selectionText is not null)
        {
            return new AttachmentSelection
            {
                FilePath = FilePath,
                DisplayName = DisplayName,
                Text = _selectionText,
                Selection = new AttachmentSelectionDetails
                {
                    Start = new AttachmentSelectionDetailsStart { Line = s.StartLine, Character = s.StartColumn },
                    End = new AttachmentSelectionDetailsEnd { Line = s.EndLine, Character = s.EndColumn }
                }
            };
        }

        return new AttachmentFile
        {
            Path = FilePath,
            DisplayName = DisplayName
        };
    }

    public void Dispose()
    {
        ThumbnailBitmap?.Dispose();
    }

    public readonly record struct SelectionRange(int StartLine, int StartColumn, int EndLine, int EndColumn);
}
