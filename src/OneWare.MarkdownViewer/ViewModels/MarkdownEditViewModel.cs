using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OneWare.Core.Services;
using OneWare.Core.ViewModels.DockViews;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.MarkdownViewer.ViewModels;

/// <summary>
///     An <see cref="EditViewModel" /> for markdown files that adds a live preview pane.
///     The editor and the preview can be toggled independently to allow editor-only,
///     preview-only or split-screen layouts.
/// </summary>
public class MarkdownEditViewModel : EditViewModel
{
    private readonly CompositeDisposable _markdownComposite = new();

    private bool _showEditor = true;
    private bool _showPreview = true;
    private string? _markdownText;

    public MarkdownEditViewModel(string fullPath, ILogger logger, IFileIconService fileIconService,
        ISettingsService settingsService, IMainDockService mainDockService, ILanguageManager languageManager,
        IWindowService windowService, IProjectExplorerService projectExplorerService, IErrorService errorService,
        BackupService backupService) : base(fullPath, logger, fileIconService, settingsService, mainDockService,
        languageManager, windowService, projectExplorerService, errorService, backupService)
    {
        ToggleEditorCommand = new RelayCommand(() => ShowEditor = !ShowEditor);
        TogglePreviewCommand = new RelayCommand(() => ShowPreview = !ShowPreview);

        // Keep the preview in sync with the editor content. The document instance can be
        // replaced when the file is (re)loaded, so we listen to both events.
        Editor.DocumentChanged += OnEditorChanged;

        Observable.FromEventPattern(
                h => Editor.TextChanged += h,
                h => Editor.TextChanged -= h)
            .Throttle(TimeSpan.FromMilliseconds(250))
            .Subscribe(_ => UpdateMarkdown())
            .DisposeWith(_markdownComposite);
    }

    public RelayCommand ToggleEditorCommand { get; }

    public RelayCommand TogglePreviewCommand { get; }

    public string? MarkdownText
    {
        get => _markdownText;
        private set => SetProperty(ref _markdownText, value);
    }

    public bool ShowEditor
    {
        get => _showEditor;
        set
        {
            // Always keep at least one pane visible.
            if (!value && !ShowPreview) ShowPreview = true;
            if (SetProperty(ref _showEditor, value))
            {
                OnPropertyChanged(nameof(IsSplitterVisible));
                OnPropertyChanged(nameof(EditorColumnWidth));
            }
        }
    }

    public bool ShowPreview
    {
        get => _showPreview;
        set
        {
            // Always keep at least one pane visible.
            if (!value && !ShowEditor) ShowEditor = true;
            if (SetProperty(ref _showPreview, value))
            {
                OnPropertyChanged(nameof(IsSplitterVisible));
                OnPropertyChanged(nameof(PreviewColumnWidth));
                if (value) UpdateMarkdown();
            }
        }
    }

    public bool IsSplitterVisible => ShowEditor && ShowPreview;

    /// <summary>Star width when the editor is visible, collapsed otherwise.</summary>
    public GridLength EditorColumnWidth =>
        ShowEditor ? new GridLength(1, GridUnitType.Star) : new GridLength(0, GridUnitType.Auto);

    /// <summary>Star width when the preview is visible, collapsed otherwise.</summary>
    public GridLength PreviewColumnWidth =>
        ShowPreview ? new GridLength(1, GridUnitType.Star) : new GridLength(0, GridUnitType.Auto);

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        UpdateMarkdown();
    }

    private void UpdateMarkdown()
    {
        if (Dispatcher.UIThread.CheckAccess())
            MarkdownText = Editor.Document?.Text ?? string.Empty;
        else
            Dispatcher.UIThread.Post(() => MarkdownText = Editor.Document?.Text ?? string.Empty);
    }

    public override bool OnClose()
    {
        var result = base.OnClose();
        if (!result) return false;

        Editor.DocumentChanged -= OnEditorChanged;
        _markdownComposite.Dispose();
        return true;
    }
}






