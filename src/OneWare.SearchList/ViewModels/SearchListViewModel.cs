using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using DynamicData;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.Helpers;
using OneWare.SearchList.Models;
using ReactiveUI;

namespace OneWare.SearchList.ViewModels;

public partial class SearchListViewModel : ExtendedTool
{
    public const string IconKey = "VsImageLib.Search16XMd";
    private const long MaxTextFileBytes = 2 * 1024 * 1024;

    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;

    private CancellationTokenSource? _lastCancellationToken;

    public SearchListViewModel(IMainDockService mainDockService, IProjectExplorerService projectExplorerService) :
        base(IconKey)
    {
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;

        Title = "Find in files";
        Id = "Search";

        this.WhenAnyValue(x => x.SearchString)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(Search);
    }

    public string SearchString
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<SearchResultModel> Items { get; } = new();

    public SearchResultModel? SelectedItem
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsLoading
    {
        get;
        set => SetProperty(ref field, value);
    }

    [DataMember]
    public bool IsReplaceVisible
    {
        get;
        set
        {
            SetProperty(ref field, value);
            Title = value ? "Replace" : "Find";
        }
    }

    [DataMember]
    public string ReplaceString
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    [DataMember]
    public bool CaseSensitive
    {
        get;
        set => SetProperty(ref field, value);
    }

    [DataMember]
    public bool WholeWord
    {
        get;
        set => SetProperty(ref field, value);
    }

    [DataMember]
    public bool UseRegex
    {
        get;
        set
        {
            if (SetProperty(ref field, value)) OnPropertyChanged(nameof(ReplaceWatermark));
        }
    }

    public string ReplaceWatermark => UseRegex ? @"Replace... ($1 or \1 for capture groups)" : "Replace...";

    [DataMember]
    public int SearchListFilterMode
    {
        get;
        set => SetProperty(ref field, value);
    } = 1;

    private void Search(string searchText)
    {
        Items.Clear();
        _lastCancellationToken?.Cancel();
        if (searchText.Length < 3)
        {
            IsLoading = false;
            return;
        }
        _ = SearchAsync(searchText);
    }

    private async Task SearchAsync(string searchText)
    {
        // An incomplete regex while typing simply shows no results
        if (BuildSearchRegex(searchText) is not { } regex) return;

        IsLoading = true;
        _lastCancellationToken = new CancellationTokenSource();
        var token = _lastCancellationToken.Token;

        try
        {
            switch (SearchListFilterMode)
            {
                case 0:
                    foreach (var project in _projectExplorerService.Projects)
                    {
                        await SearchProjectFilesAsync(project, regex, token);
                        if (token.IsCancellationRequested) return;
                    }
                    break;
                case 1 when _projectExplorerService.ActiveProject != null:
                    await SearchProjectFilesAsync(_projectExplorerService.ActiveProject, regex, token);
                    break;
                case 2 when _mainDockService.CurrentDocument is IEditor editor:
                    Items.AddRange(await FindAllIndexesAsync(editor.FullPath, null, regex, token));
                    break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchProjectFilesAsync(IProjectFolder folder, Regex regex, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested) return;

        foreach (var relativePath in folder.GetFiles("*", true))
        {
            if (cancel.IsCancellationRequested) return;

            var fullPath = Path.Combine(folder.FullPath, relativePath);

            if (!IsBinaryFile(fullPath) && !IsTooLarge(fullPath))
                Items.AddRange(await FindAllIndexesAsync(fullPath, folder.Root, regex, cancel));
        }
    }

    private static bool IsTooLarge(string fullPath)
    {
        try
        {
            return new FileInfo(fullPath).Length > MaxTextFileBytes;
        }
        catch
        {
            return true;
        }
    }

    private static bool IsBinaryFile(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        if (!string.IsNullOrEmpty(extension))
        {
            switch (extension.ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".gif":
                case ".bmp":
                case ".tiff":
                case ".ico":
                case ".svg":
                case ".webp":
                case ".mp3":
                case ".wav":
                case ".flac":
                case ".ogg":
                case ".mp4":
                case ".mkv":
                case ".mov":
                case ".avi":
                case ".wmv":
                case ".zip":
                case ".7z":
                case ".rar":
                case ".gz":
                case ".tar":
                case ".tgz":
                case ".pdf":
                case ".exe":
                case ".dll":
                case ".so":
                case ".dylib":
                case ".bin":
                case ".dat":
                case ".class":
                case ".jar":
                case ".pdb":
                case ".o":
                case ".obj":
                case ".a":
                case ".lib":
                case ".woff":
                case ".woff2":
                case ".ttf":
                case ".otf":
                    return true;
            }
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            var buffer = new byte[1024];
            var read = stream.Read(buffer, 0, buffer.Length);
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == 0) return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static async Task<IList<SearchResultModel>> FindAllIndexesAsync(string fullPath, IProjectRoot? root,
        Regex regex, CancellationToken cancellationToken)
    {
        if (!File.Exists(fullPath)) return new List<SearchResultModel>();

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return await Task.Run(() => SearchReplaceHelper.FindMatches(text, regex, cancellationToken)
            .Select(m => new SearchResultModel(m.Line.Trim(), m.Left, m.Match, m.Right, regex.ToString(),
                root, fullPath, m.LineNumber, m.StartOffset, m.Length))
            .ToList() as IList<SearchResultModel>, cancellationToken);
    }

    private Regex? BuildSearchRegex(string searchText)
    {
        return SearchReplaceHelper.TryBuildRegex(searchText, CaseSensitive, UseRegex, WholeWord);
    }

    public void OpenSelectedResult()
    {
        if (SelectedItem == null) return;
        _ = GoToSearchResultAsync(SelectedItem);
    }

    [RelayCommand]
    private async Task ReplaceSelectedAsync()
    {
        var result = SelectedItem;
        if (result == null) return;
        if (string.IsNullOrWhiteSpace(result.FilePath)) return;
        if (result.EndOffset <= result.StartOffset) return;
        if (IsBinaryFile(result.FilePath) || IsTooLarge(result.FilePath)) return;
        if (BuildSearchRegex(SearchString) is not { } regex) return;

        var text = await File.ReadAllTextAsync(result.FilePath);
        var newText = SearchReplaceHelper.ReplaceAt(text, regex, result.StartOffset,
            result.EndOffset - result.StartOffset, ReplaceString ?? string.Empty, UseRegex);

        // null means the file changed since the search, so refresh the results instead of replacing stale offsets
        if (newText != null)
            await File.WriteAllTextAsync(result.FilePath, newText);
        Search(SearchString);
    }

    [RelayCommand]
    private async Task ReplaceAllAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchString)) return;
        if (Items.Count == 0) return;
        if (BuildSearchRegex(SearchString) is not { } regex) return;

        var files = Items
            .Select(x => x.FilePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (files.Count == 0) return;

        var replacement = ReplaceString ?? string.Empty;

        foreach (var file in files)
        {
            if (file == null) continue;
            if (IsBinaryFile(file) || IsTooLarge(file)) continue;

            var text = await File.ReadAllTextAsync(file);
            var replaced = SearchReplaceHelper.ReplaceAll(text, regex, replacement, UseRegex);
            if (text != replaced)
                await File.WriteAllTextAsync(file, replaced);
        }

        Search(SearchString);
    }

    private async Task GoToSearchResultAsync(SearchResultModel resultModel)
    {
        if (string.IsNullOrWhiteSpace(resultModel?.FilePath)) return;

        if (await _mainDockService.OpenFileAsync(resultModel.FilePath) is not IEditor evb) return;

        if (_mainDockService.GetWindowOwner(this) is IHostWindow)
            _mainDockService.CloseDockable(this);

        //JUMP TO LINE
        if (resultModel.Line > 0)
        {
            if (resultModel is { StartOffset: 0, EndOffset: 0 })
            {
                if (resultModel.Line <= evb.CurrentDocument.LineCount)
                {
                    var line = evb.CurrentDocument.GetLineByNumber(resultModel.Line);
                    evb.Select(line.Offset, line.EndOffset - line.Offset);
                }
            }
            else
            {
                evb.Select(resultModel.StartOffset, resultModel.EndOffset - resultModel.StartOffset);
            }
        }

        if (evb.Owner?.Owner is IRootDock { Window: { Host: Window win } }) win.Activate();
    }
}
