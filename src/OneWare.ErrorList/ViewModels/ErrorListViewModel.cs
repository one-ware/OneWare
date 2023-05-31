using System.Collections.ObjectModel;
using Avalonia.Collections;
using Dock.Model.Mvvm.Controls;
using DynamicData.Binding;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using ListEx = DynamicData.ListEx;

namespace OneWare.ErrorList.ViewModels
{
    public enum ErrorListFilterMode
    {
        All,
        CurrentProject,
        CurrentFile,
    }
    
    public class ErrorListViewModel : Tool, IErrorService
    {
        private readonly IDockService _dockService;
        private readonly ISettingsService _settingsService;
        private readonly IProjectService _projectExplorerViewModel;

        public List<string> ErrorListVisibleSources = new List<string>();
        
        private ErrorListFilterMode _errorListFilterMode;
        public ErrorListFilterMode ErrorListFilterMode
        {
            get => _errorListFilterMode;
            set => SetProperty(ref _errorListFilterMode, value);
        }

        private bool _showExternalErrors = true;
        public bool ShowExternalErrors
        {
            get => _showExternalErrors;
            set => SetProperty(ref _showExternalErrors, value);
        }

        private string? _errorListVisibleSource;
        public string? ErrorListVisibleSource
        {
            get => _errorListVisibleSource;
            set => SetProperty(ref _errorListVisibleSource, value);
        }
        
        private int _errorCount;
        public string ErrorCountVisible
        {
            get
            {
                if (ErrorEnabled) return _errorCount + " Errors";
                return "0 of " + _errorCount + " Errors";
            }
        }

        private int _warningCount;

        public string WarningCountVisible
        {
            get
            {
                if (WarningEnabled) return _warningCount + " Warnings";
                return "0 of " + _warningCount + " Warnings";
            }
        }

        private int _hintCount;

        public string HintCountVisible
        {
            get
            {
                if (HintEnabled) return _hintCount + " Hints";
                return "0 of " + _hintCount + " Hints";
            }
        }
        
        private bool _errorEnabled = true;

        public bool ErrorEnabled
        {
            get => _errorEnabled;
            set
            {
                SetProperty(ref _errorEnabled, value);
                ErrorRefresh?.Invoke(this, null);
                Filter();
            }
        }

        private bool _warningEnabled = true;

        public bool WarningEnabled
        {
            get => _warningEnabled;
            set
            {
                SetProperty(ref _warningEnabled, value);
                ErrorRefresh?.Invoke(this, null);
                Filter();
            }
        }

        private bool _hintEnabled = true;

        public bool HintEnabled
        {
            get => _hintEnabled;
            set
            {
                SetProperty(ref _hintEnabled, value);
                ErrorRefresh?.Invoke(this, null);
                Filter();
            }
        }

        private readonly ObservableCollection<ErrorListItemModel> _items = new();
        public DataGridCollectionView Collection { get; }
        public string SearchString { get; set; } = "";

        private ErrorListItemModel? _selectedItem;
        public ErrorListItemModel? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }
        
        public event EventHandler<object?>? ErrorRefresh;

        public ErrorListViewModel(IDockService dockService, ISettingsService settingsService, IProjectService projectExplorerViewModel)
        {
            _dockService = dockService;
            _settingsService = settingsService;
            _projectExplorerViewModel = projectExplorerViewModel;
            
            Id = "ErrorList";
            Title = "Code Errors";
                
            Collection = new DataGridCollectionView(_items, false, true)
            {
                Filter = Filter
            };

            _settingsService.Bind(ErrorListModule.KeyErrorListFilterMode, this.WhenValueChanged(x => x.ErrorListFilterMode))
                .Subscribe(x => ErrorListFilterMode = x);
        }

        private bool Filter(object arg)
        {
            if (arg is not ErrorListItemModel error) return false;
            var f = FilterMode(error) && FilterErrorSource(error) &&
                   FilterSearchString(error) && (ShowExternalErrors || error.File is IProjectFile || _dockService.OpenFiles.ContainsKey(error.File));

            return f && FilterEnabledType(error);
        }

        private bool FilterMode(ErrorListItemModel error)
        {
            switch (ErrorListFilterMode)
            {
                case ErrorListFilterMode.All:
                    return true;
                case ErrorListFilterMode.CurrentProject:
                    if (error.File is IProjectFile pf && _projectExplorerViewModel.ActiveProject == pf.Root) return true;
                    break;
                case ErrorListFilterMode.CurrentFile:
                    if ((_dockService.CurrentDocument as IEditor)?.CurrentFile == error.File) return true;
                    break;
            }
            return false;
        }

        private bool FilterEnabledType(ErrorListItemModel error)
        {
            switch (error.Type)
            {
                case ErrorType.Error when ErrorEnabled:
                case ErrorType.Warning when WarningEnabled:
                case ErrorType.Hint when WarningEnabled:
                    return true;
                default:
                    return false;
            }
        }

        private bool FilterErrorSource(ErrorListItemModel error)
        {
            return ErrorListVisibleSource == null || ErrorListVisibleSource == error.Source;
        }

        private bool FilterSearchString(ErrorListItemModel error)
        {
            if (string.IsNullOrWhiteSpace(SearchString)) return true;
            return error.Description.Contains(SearchString, StringComparison.OrdinalIgnoreCase) || (error.Code?.Contains(SearchString, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private int CountToggle(ErrorType type)
        {
            return _items.Count(error => error.Type == type && FilterMode(error) && FilterErrorSource(error) && FilterSearchString(error));

        }
        
        public void Filter()
        {
            Collection.Refresh();
            RefreshCountToggle();
        }

        private void RefreshCountToggle()
        {
            _errorCount = CountToggle(ErrorType.Error);
            _hintCount = CountToggle(ErrorType.Hint);
            _warningCount = CountToggle(ErrorType.Warning);
            
            OnPropertyChanged(nameof(ErrorCountVisible));
            OnPropertyChanged(nameof(WarningCountVisible));
            OnPropertyChanged(nameof(HintCountVisible));
            
            Collection.SortDescriptions.Clear();
        }
        
        public void Clear(IProjectRoot project)
        {
            ListEx.RemoveMany(_items, _items.Where(x => x.File is IProjectFile pf && pf.Root == project));
            ErrorRefresh?.Invoke(this, project);
            RefreshCountToggle();
        }

        public void Clear(IProjectRoot project, string source)
        {
            ListEx.RemoveMany(_items, _items.Where(x => x.File is IProjectFile pf && pf.Root == project && x.Source == source));
            ErrorRefresh?.Invoke(this, project);
            RefreshCountToggle();
        }

        public void Clear(IFile file)
        {
            ListEx.RemoveMany(_items, _items.Where(x => x.File == file));
            ErrorRefresh?.Invoke(this, file);
            RefreshCountToggle();
        }

        public void Clear(string source)
        {
            ListEx.RemoveMany(_items, _items.Where(x => x.Source == source));
            
            ErrorRefresh?.Invoke(this, null);
            RefreshCountToggle();
        }
        
        public IEnumerable<ErrorListItemModel> GetErrorsForFile(IFile file)
        {
            foreach (var error in _items.Where(x => x.File == file))
            {
                if (ErrorEnabled && error.Type == ErrorType.Error) yield return error;
                if (WarningEnabled && error.Type == ErrorType.Warning) yield return error;
                if (HintEnabled && error.Type == ErrorType.Hint) yield return error;
            }
        }

        /// <summary>
        /// Adds new Errors and filters old errors out
        /// </summary>
        public void RefreshErrors(IList<ErrorListItemModel> errors, string source, IFile entry)
        {
            ListEx.RemoveMany(_items, _items.Where(x => (x.File == entry) && x.Source == source && !errors.Contains(x)));
            
            foreach (var e in errors)
            {
                Add(e);
            }
            
            ErrorRefresh?.Invoke(this, entry);
            RefreshCountToggle();
        }

        public void Add(ErrorListItemModel entry)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                var comparison = entry.CompareTo(_items[i]);
                switch (comparison)
                {
                    case 0: //Items equal
                        return;
                    case < 0:
                        _items.Insert(i, entry);
                        return;
                }
            }
            
            _items.Add(entry);
        }
        
        public async Task GoToErrorAsync()
        {
            if (SelectedItem is not { } error) return;
            var doc = await _dockService.OpenFileAsync(error.File);
            
            if (doc is not IEditor evb) return;
            var offset = error.GetOffset(evb.CurrentDocument);
            evb.Select(offset.startOffset, offset.endOffset - offset.startOffset);
        }
    }
}