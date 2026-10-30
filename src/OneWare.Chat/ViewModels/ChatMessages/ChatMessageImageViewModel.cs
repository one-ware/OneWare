using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;

namespace OneWare.Chat.ViewModels.ChatMessages;

/// <summary>An image a tool returned to the AI, stored next to the chat history.</summary>
public class ChatMessageImageViewModel : ObservableObject, IChatMessage, IEstimatedHeightItem
{
    /// <summary>Maximum width the preview is decoded to, which keeps large screenshots cheap in memory.</summary>
    private const int PreviewDecodeWidth = 1024;

    private const double PreviewMaxHeight = 320;

    private bool _loaded;

    public ChatMessageImageViewModel(string filePath, string mimeType)
    {
        FilePath = filePath;
        MimeType = mimeType;
    }

    public string FilePath { get; }

    public string MimeType { get; }

    public string? Description { get; init; }

    public string? ToolName { get; init; }

    public string Caption => !string.IsNullOrWhiteSpace(Description) ? Description
        : !string.IsNullOrWhiteSpace(ToolName) ? $"Image from {ToolName}"
        : "Image";

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>The decoded preview, or null when the file is missing or cannot be decoded.</summary>
    public Bitmap? Image
    {
        get
        {
            if (_loaded) return field;
            _loaded = true;

            try
            {
                if (!File.Exists(FilePath)) return null;

                using var stream = File.OpenRead(FilePath);
                var bitmap = new Bitmap(stream);
                if (bitmap.PixelSize.Width > PreviewDecodeWidth)
                {
                    var height = Math.Max(1, bitmap.PixelSize.Height * PreviewDecodeWidth / bitmap.PixelSize.Width);
                    var scaled = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(PreviewDecodeWidth, height));
                    bitmap.Dispose();
                    bitmap = scaled;
                }

                field = bitmap;
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<Microsoft.Extensions.Logging.ILogger>()
                    ?.Warning($"Showing chat image '{FilePath}' failed", e);
                field = null;
            }

            return field;
        }
    }

    public bool HasImage => Image != null;

    public void Open()
    {
        if (File.Exists(FilePath)) PlatformHelper.OpenHyperLink(FilePath);
    }

    public double EstimateHeight(double width) => PreviewMaxHeight + 24;
}
