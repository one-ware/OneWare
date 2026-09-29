using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.ErrorList.ViewModels;

public enum ErrorListFilterMode
{
    All,
    CurrentProject,
    CurrentFile
}

public class ErrorListViewModel : ExtendedTool, IErrorService
{
    public const string IconKey = "MaterialDesign.ErrorOutline";
    public const string AllSources = "All Sources";

    private readonly BatchObservableCollection<ErrorListFileNode> _files = new();
    private readonly IFileIconService _fileIconService;
    private readonly List<ErrorListItem> _items = new();

    private readonly IMainDockService _mainDockService;
    private readonly IProjectExplorerService _projectExplorerService;

    private int _errorCount;
    private bool _errorEnabled = true;
    private ErrorListFilterMode _errorListFilterMode;
    private string? _errorListVisibleSource = AllSources;
    private int _hintCount;
    private bool _hintEnabled = true;
    private bool _refreshScheduled;
    private string _searchString = string.Empty;
    private bool _showExternalErrors = true;
    private int _visibleCount;
    private int _warningCount;
    private bool _warningEnabled = true;

    public ErrorListViewModel(IMainDockService mainDockService, ISettingsService settingsService,
        IProjectExplorerService projectExplorerService, IFileIconService fileIconService) : base(IconKey)
    {
        _mainDockService = mainDockService;
        _projectExplorerService = projectExplorerService;
        _fileIconService = fileIconService;

        Id = "Problems";
        Title = "Problems";

        Source = new HierarchicalTreeDataGridSource<ErrorListNode>(_files)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ErrorListNode>(
                    new TemplateColumn<ErrorListNode>("Description", "ErrorListDescriptionTemplate", null,
                        new GridLength(1, GridUnitType.Star)),
                    x => x.Children, x => x.HasChildren, x => x.IsExpanded),
                new TextColumn<ErrorListNode, string?>("Code", x => x.Code, new GridLength(100)),
                new TextColumn<ErrorListNode, string?>("Source", x => x.Source, new GridLength(120)),
                new TextColumn<ErrorListNode, string?>("Line", x => x.Location, new GridLength(70))
            }
        };

        ExpandAllCommand = new RelayCommand(() => SetAllExpanded(true));
        CollapseAllCommand = new RelayCommand(() => SetAllExpanded(false));

        Observable.FromEventPattern<IProjectRoot>(
                h => projectExplorerService.ProjectRemoved += h,
                h => projectExplorerService.ProjectRemoved -= h)
            .Subscribe(x => Clear(x.EventArgs));

        settingsService.Bind(ErrorListModule.KeyErrorListFilterMode, this.WhenValueChanged(x => x.ErrorListFilterMode))
            .Subscribe(x => ErrorListFilterMode = x);
        settingsService.Bind(ErrorListModule.KeyErrorListShowExternalErrors,
                this.WhenValueChanged(x => x.ShowExternalErrors))
            .Subscribe(x => ShowExternalErrors = x);

        _mainDockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(_ =>
        {
            if (ErrorListFilterMode == ErrorListFilterMode.CurrentFile || !ShowExternalErrors) Filter();
        });
        projectExplorerService.WhenValueChanged(x => x.ActiveProject).Subscribe(_ =>
        {
            if (ErrorListFilterMode == ErrorListFilterMode.CurrentProject) Filter();
        });
    }

    public HierarchicalTreeDataGridSource<ErrorListNode> Source { get; }

    public IRelayCommand ExpandAllCommand { get; }
    public IRelayCommand CollapseAllCommand { get; }

    public ObservableCollection<string> ErrorListVisibleSources { get; } = new() { AllSources };

    public ErrorListFilterMode ErrorListFilterMode
    {
        get => _errorListFilterMode;
        set
        {
            if (SetProperty(ref _errorListFilterMode, value)) Filter();
        }
    }

    public bool ShowExternalErrors
    {
        get => _showExternalErrors;
        set
        {
            if (SetProperty(ref _showExternalErrors, value)) Filter();
        }
    }

    public string? ErrorListVisibleSource
    {
        get => _errorListVisibleSource;
        set
        {
            if (SetProperty(ref _errorListVisibleSource, value)) Filter();
        }
    }

    public string SearchString
    {
        get => _searchString;
        set
        {
            if (SetProperty(ref _searchString, value ?? string.Empty)) Filter();
        }
    }

    /// <summary>
    ///     Number of errors that pass the scope, source and search filters (independent of the severity toggles).
    /// </summary>
    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public int HintCount
    {
        get => _hintCount;
        private set => SetProperty(ref _hintCount, value);
    }

    /// <summary>
    ///     Number of problems currently shown in the tree.
    /// </summary>
    public int VisibleCount
    {
        get => _visibleCount;
        private set
        {
            if (!SetProperty(ref _visibleCount, value)) return;
            OnPropertyChanged(nameof(HasVisibleProblems));
            OnPropertyChanged(nameof(EmptyText));
        }
    }

    public bool HasVisibleProblems => VisibleCount > 0;

    public string EmptyText => _items.Count == 0 ? "No problems found" : "No problems match the current filters";

    public bool ErrorEnabled
    {
        get => _errorEnabled;
        set => SetTypeEnabled(ref _errorEnabled, value);
    }

    public bool WarningEnabled
    {
        get => _warningEnabled;
        set => SetTypeEnabled(ref _warningEnabled, value);
    }

    public bool HintEnabled
    {
        get => _hintEnabled;
        set => SetTypeEnabled(ref _hintEnabled, value);
    }

    public ErrorListNode? SelectedNode => Source.RowSelection?.SelectedItem;

    public event EventHandler<object?>? ErrorRefresh;

    public void RegisterErrorSource(string source)
    {
        if (!ErrorListVisibleSources.Contains(source)) ErrorListVisibleSources.Add(source);
    }

    public void ClearFile(string filePath)
    {
        _items.RemoveAll(x => x.FilePath.EqualPaths(filePath));

        ErrorRefresh?.Invoke(this, filePath);
        Filter();
    }

    public void Clear(string source)
    {
        var files = _items.Where(x => x.Source == source).Select(x => x.FilePath).Distinct().ToList();
        _items.RemoveAll(x => x.Source == source);

        foreach (var file in files) ErrorRefresh?.Invoke(this, file);
        Filter();
    }

    public IEnumerable<ErrorListItem> GetErrors()
    {
        return _items;
    }

    public IEnumerable<ErrorListItem> GetErrorsForFile(string filePath)
    {
        return _items.Where(x => x.FilePath.EqualPaths(filePath) && IsTypeEnabled(x.Type)).ToList();
    }

    /// <summary>
    ///     Adds new Errors and filters old errors out
    /// </summary>
    public void RefreshErrors(IList<ErrorListItem> errors, string source, string filePath)
    {
        var newErrors = errors.ToHashSet();
        _items.RemoveAll(x => x.FilePath.EqualPaths(filePath) && x.Source == source && !newErrors.Contains(x));

        var existing = _items.Where(x => x.FilePath.EqualPaths(filePath)).ToHashSet();
        foreach (var error in errors)
            if (existing.Add(error))
                _items.Add(error);

        ErrorRefresh?.Invoke(this, filePath);
        Filter();
    }

    public void Clear(IProjectRoot project)
    {
        _items.RemoveAll(x => x.Root == project);

        ErrorRefresh?.Invoke(this, project);
        Filter();
    }

    public void Clear(IProjectRoot project, string source)
    {
        _items.RemoveAll(x => x.Root == project && x.Source == source);

        ErrorRefresh?.Invoke(this, project);
        Filter();
    }

    public void Add(ErrorListItem entry)
    {
        if (_items.Contains(entry)) return;
        _items.Add(entry);
        Filter();
    }

    /// <summary>
    ///     Schedules a refresh of the tree and the counters. Multiple calls in a row are coalesced.
    /// </summary>
    public void Filter()
    {
        if (_refreshScheduled) return;
        _refreshScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshScheduled = false;
            RefreshTree();
        }, DispatcherPriority.Background);
    }

    public async Task GoToErrorAsync()
    {
        switch (SelectedNode)
        {
            case ErrorListProblemNode problem:
                var doc = await _mainDockService.OpenFileAsync(problem.Item.FilePath);
                doc?.GoToDiagnostic(problem.Item);
                break;
            case ErrorListFileNode file:
                await _mainDockService.OpenFileAsync(file.FilePath);
                break;
        }
    }

    private void SetTypeEnabled(ref bool field, bool value)
    {
        if (!SetProperty(ref field, value)) return;
        ErrorRefresh?.Invoke(this, null);
        Filter();
    }

    private void SetAllExpanded(bool expanded)
    {
        foreach (var file in _files) file.IsExpanded = expanded;
        if (expanded) Source.ExpandAll();
        else Source.CollapseAll();
    }

    private void RefreshTree()
    {
        int errors = 0, warnings = 0, hints = 0;
        var groups = new Dictionary<string, List<ErrorListItem>>(StringComparer.Ordinal);

        foreach (var item in _items)
        {
            if (!FilterScope(item) || !FilterSource(item) || !FilterSearchString(item) || !FilterExternal(item))
                continue;

            switch (item.Type)
            {
                case ErrorType.Error: errors++; break;
                case ErrorType.Warning: warnings++; break;
                case ErrorType.Hint: hints++; break;
            }

            if (!IsTypeEnabled(item.Type)) continue;

            var key = item.FilePath.ToPathKey();
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<ErrorListItem>();
            list.Add(item);
        }

        ErrorCount = errors;
        WarningCount = warnings;
        HintCount = hints;

        var fileKeys = groups
            .OrderBy(x => Path.GetFileName(x.Value[0].FilePath), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Value[0].FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key)
            .ToList();

        CollectionSync.Sync(_files, fileKeys, x => x.FilePath.ToPathKey(), key =>
        {
            var first = groups[key][0];
            return new ErrorListFileNode(first.FilePath, first.Root,
                _fileIconService.GetFileIconModel(first.FileExtension));
        }, StringComparer.Ordinal);

        foreach (var file in _files)
        {
            var problems = groups[file.FilePath.ToPathKey()];
            problems.Sort(CompareProblems);
            CollectionSync.Sync(file.ProblemNodes, problems, x => x.Item, item => new ErrorListProblemNode(item, file),
                EqualityComparer<ErrorListItem>.Default);
            file.UpdateCounts();
        }

        VisibleCount = groups.Values.Sum(x => x.Count);
        OnPropertyChanged(nameof(EmptyText));
    }

    private static int SeverityRank(ErrorType type)
    {
        return type switch
        {
            ErrorType.Error => 0,
            ErrorType.Warning => 1,
            _ => 2
        };
    }

    private static int CompareProblems(ErrorListItem a, ErrorListItem b)
    {
        var result = SeverityRank(a.Type).CompareTo(SeverityRank(b.Type));
        if (result != 0) return result;
        result = a.StartLine.CompareTo(b.StartLine);
        if (result != 0) return result;
        result = (a.StartColumn ?? 0).CompareTo(b.StartColumn ?? 0);
        if (result != 0) return result;
        result = string.Compare(a.Description, b.Description, StringComparison.Ordinal);
        if (result != 0) return result;
        result = string.Compare(a.Source, b.Source, StringComparison.Ordinal);
        return result != 0 ? result : string.Compare(a.Code, b.Code, StringComparison.Ordinal);
    }

    private bool IsTypeEnabled(ErrorType type)
    {
        return type switch
        {
            ErrorType.Error => ErrorEnabled,
            ErrorType.Warning => WarningEnabled,
            ErrorType.Hint => HintEnabled,
            _ => true
        };
    }

    private bool FilterScope(ErrorListItem error)
    {
        return ErrorListFilterMode switch
        {
            ErrorListFilterMode.CurrentProject => error.Root != null &&
                                                  _projectExplorerService.ActiveProject == error.Root,
            ErrorListFilterMode.CurrentFile => _mainDockService.CurrentDocument?.FullPath.EqualPaths(error.FilePath) ??
                                               false,
            _ => true
        };
    }

    private bool FilterExternal(ErrorListItem error)
    {
        return ShowExternalErrors || error.Root != null ||
               _mainDockService.OpenFiles.ContainsKey(error.FilePath.ToPathKey());
    }

    private bool FilterSource(ErrorListItem error)
    {
        return ErrorListVisibleSource is null or AllSources || ErrorListVisibleSource == error.Source;
    }

    private bool FilterSearchString(ErrorListItem error)
    {
        if (string.IsNullOrWhiteSpace(SearchString)) return true;
        var search = SearchString.Trim();
        return error.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
               || error.FilePath.Contains(search, StringComparison.OrdinalIgnoreCase)
               || (error.Root?.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (error.Source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
               || (error.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
