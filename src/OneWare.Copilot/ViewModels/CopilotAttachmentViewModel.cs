using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitHub.Copilot;

namespace OneWare.Copilot.ViewModels;

/// <summary>
/// A single attachment chip shown in the Copilot chat attachment strip. Represents either a whole
/// file or a code selection within a file, and knows how to convert itself into an SDK
/// <see cref="Attachment"/> when a message is sent.
/// </summary>
public sealed class CopilotAttachmentViewModel : ObservableObject
{
    private readonly string? _selectionText;
    private readonly SelectionRange? _selection;

    public string FilePath { get; }

    public string DisplayName { get; }

    /// <summary>Short line-range hint shown on the chip, e.g. "L10-24". Null for whole-file attachments.</summary>
    public string? Detail { get; }

    /// <summary>True for the implicit, auto-tracked focused-file chip.</summary>
    public bool IsActiveFile { get; }

    public string IconResourceKey { get; }

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

    public Attachment ToSdkAttachment()
    {
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

    public readonly record struct SelectionRange(int StartLine, int StartColumn, int EndLine, int EndColumn);
}
