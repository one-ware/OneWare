using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.ReactiveUI;
using Dock.Model.Controls;
using Dock.Model.Core;
using DynamicData;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.SearchList.Models;
using ReactiveUI;

namespace OneWare.SearchList.ViewModels;

public class SearchListViewModel : ExtendedTool
{
    public const string IconKey = "VsImageLib.Search16XMd";

    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;

    private bool _caseSensitive;

    private bool _isLoading;

    private CancellationTokenSource? _lastCancellationToken;

    private int _searchListFilterMode = 1;

    private string _searchString = string.Empty;

    private SearchResultModel? _selectedItem;

    private bool _useRegex;

    private bool _wholeWord;

    public SearchListViewModel(IMainDockService mainDockService, IProjectExplorerService projectExplorerService) :
        base(IconKey)
    {
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;

        Title = "Search";
        Id = "Search";

        this.WhenAnyValue(x => x.SearchString)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(Search);
    }

    public string SearchString
    {
        get => _searchString;
        set => SetProperty(ref _searchString, value);
    }

    public ObservableCollection<SearchResultModel> Items { get; } = new();

    public SearchResultModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    [DataMember]
    public bool CaseSensitive
    {
        get => _caseSensitive;
        set => SetProperty(ref _caseSensitive, value);
    }

    [DataMember]
    public bool WholeWord
    {
        get => _wholeWord;
        set => SetProperty(ref _wholeWord, value);
    }

    [DataMember]
    public bool UseRegex
    {
        get => _useRegex;
        set => SetProperty(ref _useRegex, value);
    }

    [DataMember]
    public int SearchListFilterMode
    {
        get => _searchListFilterMode;
        set => SetProperty(ref _searchListFilterMode, value);
    }

    private void Search(string searchText)
    {
        Items.Clear();
        _lastCancellationToken?.Cancel();
        if (searchText.Length < 3) return;
        _ = SearchAsync(searchText);
    }

    private async Task SearchAsync(string searchText)
    {
        IsLoading = true;

        _lastCancellationToken = new CancellationTokenSource();

        switch (SearchListFilterMode)
        {
            case 0:
                await SearchFolderRecursiveAsync(_projectExplorerService.Projects, searchText,
                    _lastCancellationToken.Token);
                break;
            case 1 when _projectExplorerService.ActiveProject != null:
                await SearchFolderRecursiveAsync(_projectExplorerService.ActiveProject.Entities, searchText,
                    _lastCancellationToken.Token);
                break;
            case 2 when _mainDockService.CurrentDocument is IEditor editor:
                Items.AddRange(await FindAllIndexesAsync(editor.FullPath, null, searchText, CaseSensitive, UseRegex,
                    WholeWord, _lastCancellationToken.Token));
                break;
        }

        IsLoading = false;
    }

    private async Task SearchFolderRecursiveAsync(IEnumerable<IProjectEntry> folderItems, string searchText,
        CancellationToken cancel)
    {
        var result = new List<SearchResultModel>();
        if (cancel.IsCancellationRequested) return;
        foreach (var i in folderItems)
            switch (i)
            {
                case IProjectFile file:
                {
                    if (file.Header.Contains(searchText,
                            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                    {
                        var sI = file.Header.IndexOf(searchText,
                            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                        var dL = file.Header[..sI].TrimStart();
                        var dM = searchText;
                        var dR = file.Header[(sI + searchText.Length)..].TrimEnd();
                        Items.Add(new SearchResultModel(file.Header, dL, dM, dR, searchText,
                            file.Root, file.FullPath));
                    }

                    Items.AddRange(await FindAllIndexesAsync(file.FullPath, file.Root, searchText, CaseSensitive,
                        UseRegex, WholeWord, cancel));
                    break;
                }
                case IProjectFolder folder:
                    await SearchFolderRecursiveAsync(folder.Entities, searchText, cancel);
                    break;
            }
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

    public void OpenSelectedResult()
    {
        if (SelectedItem == null) return;
        _ = GoToSearchResultAsync(SelectedItem);
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
