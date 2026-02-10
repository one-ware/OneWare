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
        set => SetProperty(ref field, value);
    }

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
                        await SearchProjectFilesAsync(project, searchText, token);
                        if (token.IsCancellationRequested) return;
                    }
                    break;
                case 1 when _projectExplorerService.ActiveProject != null:
                    await SearchProjectFilesAsync(_projectExplorerService.ActiveProject, searchText, token);
                    break;
                case 2 when _mainDockService.CurrentDocument is IEditor editor:
                    Items.AddRange(await FindAllIndexesAsync(editor.FullPath, null, searchText, CaseSensitive, UseRegex,
                        WholeWord, token));
                    break;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchProjectFilesAsync(IProjectFolder folder, string searchText, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested) return;

        var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        foreach (var relativePath in folder.GetFiles("*", true))
        {
            if (cancel.IsCancellationRequested) return;

            var displayPath = relativePath;
            var fullPath = Path.Combine(folder.FullPath, relativePath);

            if (!IsBinaryFile(fullPath) && !IsTooLarge(fullPath))
            {
                Items.AddRange(await FindAllIndexesAsync(fullPath, folder.Root, searchText, CaseSensitive,
                    UseRegex, WholeWord, cancel));
            }
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
        string search, bool caseSensitive, bool regex, bool words, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(search) || !File.Exists(fullPath)) return new List<SearchResultModel>();

        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        var lines = text.Split('\n');
        var lastIndex = 0;
        var lastLineNr = 0;
        return await Task.Run(() =>
        {
            var indexes = new List<SearchResultModel>();
            if (regex)
            {
                var matches = Regex.Matches(text, search,
                    caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (cancellationToken.IsCancellationRequested) return indexes;
                    var index = match.Index;
                    if (index == -1) return indexes;
                    var lineNr = text[lastIndex..index].Split('\n').Length + lastLineNr - 1;
                    var line = lines[lineNr];

                    var lineM = Regex.Match(line, search,
                        caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
                    var sI = lineM.Index;
                    var dL = line[..sI].TrimStart();
                    var dM = line[sI..(sI + lineM.Length)];
                    var dR = line[(sI + lineM.Length)..].TrimEnd();
                    indexes.Add(new SearchResultModel(line.Trim(), dL, dM, dR, search,
                        root, fullPath, lineNr + 1, index, search.Length));
                    lastIndex = index;
                    lastLineNr = lineNr;
                }
            }
            else
            {
                for (var index = 0;; index += search.Length)
                {
                    if (cancellationToken.IsCancellationRequested) return indexes;
                    index = text.IndexOf(search, index,
                        caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    if (index == -1) return indexes;
                    var lineNr = text[lastIndex..index].Split('\n').Length + lastLineNr - 1;
                    var line = lines[lineNr];

                    var sI = line.IndexOf(search,
                        caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                    var dL = line[..sI].TrimStart();
                    var dM = search;
                    var dR = line[(sI + search.Length)..].TrimEnd();

                    lastIndex = index;
                    lastLineNr = lineNr;
                    if (words) //Check word boundary
                    {
                        if (index > 0 && char.IsLetterOrDigit(text[index - 1])) continue; //before
                        if (index + search.Length < text.Length &&
                            char.IsLetterOrDigit(text[index + search.Length])) continue; //before
                    }

                    indexes.Add(new SearchResultModel(line.Trim(), dL, dM, dR, search,
                        root, fullPath, lineNr + 1, index, search.Length));
                }
            }

            return indexes;
        }, cancellationToken);
    }

    private string BuildSearchPattern(string searchText)
    {
        var pattern = UseRegex ? searchText : Regex.Escape(searchText);
        if (WholeWord) pattern = $@"\b(?:{pattern})\b";
        return pattern;
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

        var text = await File.ReadAllTextAsync(result.FilePath);
        if (result.StartOffset < 0 || result.EndOffset > text.Length) return;

        var replacement = ReplaceString ?? string.Empty;
        var newText = text.Remove(result.StartOffset, result.EndOffset - result.StartOffset)
            .Insert(result.StartOffset, replacement);

        await File.WriteAllTextAsync(result.FilePath, newText);
        Search(SearchString);
    }

    [RelayCommand]
    private async Task ReplaceAllAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchString)) return;
        if (Items.Count == 0) return;

        var files = Items
            .Select(x => x.FilePath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (files.Count == 0) return;

        var pattern = BuildSearchPattern(SearchString);
        var options = CaseSensitive ? RegexOptions.Multiline : RegexOptions.Multiline | RegexOptions.IgnoreCase;
        var replacement = ReplaceString ?? string.Empty;
        Regex regex;
        try
        {
            regex = new Regex(pattern, options);
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            if (file == null) continue;
            if (IsBinaryFile(file) || IsTooLarge(file)) continue;

            var text = await File.ReadAllTextAsync(file);
            var replaced = regex.Replace(text, replacement);
            if (!ReferenceEquals(text, replaced) && text != replaced)
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
