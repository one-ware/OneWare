﻿using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia.Collections;
using DynamicData.Binding;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using ListEx = DynamicData.ListEx;

namespace OneWare.ErrorList.ViewModels
{
    public enum ErrorListFilterMode
    {
        All,
        CurrentProject,
        CurrentFile,
    }
    
    public class ErrorListViewModel : ExtendedTool, IErrorService
    {
        public const string IconKey = "MaterialDesign.ErrorOutline";
        
        private readonly IDockService _dockService;
        private readonly ISettingsService _settingsService;
        private readonly IProjectExplorerService _projectExplorerExplorerViewModel;

        public ObservableCollection<string> ErrorListVisibleSources { get; } = new(){"All Sources"};
        
        private ErrorListFilterMode _errorListFilterMode;
        public ErrorListFilterMode ErrorListFilterMode
        {
            get => _errorListFilterMode;
            set
            {
                SetProperty(ref _errorListFilterMode, value);
                Filter();
            }
        }

        private bool _showExternalErrors = true;
        public bool ShowExternalErrors
        {
            get => _showExternalErrors;
            set
            {
                SetProperty(ref _showExternalErrors, value);
                Filter();
            }
        }

        private string? _errorListVisibleSource = "All Sources";
        public string? ErrorListVisibleSource
        {
            get => _errorListVisibleSource;
            set
            {
                SetProperty(ref _errorListVisibleSource, value);
                Filter();
            }
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

        private readonly ObservableCollection<ErrorListItem> _items = new();
        public DataGridCollectionView Collection { get; }
        public string SearchString { get; set; } = "";

        private ErrorListItem? _selectedItem;
        public ErrorListItem? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }
        
        public event EventHandler<object?>? ErrorRefresh;

        public ErrorListViewModel(IDockService dockService, ISettingsService settingsService,
            IProjectExplorerService projectExplorerExplorerViewModel) : base(IconKey)
        {
            _dockService = dockService;
            _settingsService = settingsService;
            _projectExplorerExplorerViewModel = projectExplorerExplorerViewModel;
            
            Id = "Problems";
            Title = "Problems";
                
            Collection = new DataGridCollectionView(_items, false, true)
            {
                Filter = Filter
            };

            Observable.FromEventPattern<IFile>(projectExplorerExplorerViewModel,
                nameof(projectExplorerExplorerViewModel.FileRemoved)).Subscribe(
                x =>
                {
                    Clear(x.EventArgs);
                });
            
            Observable.FromEventPattern<IProjectRoot>(projectExplorerExplorerViewModel,
                nameof(projectExplorerExplorerViewModel.ProjectRemoved)).Subscribe(
                x =>
                {
                    Clear(x.EventArgs);
                });
            
            _settingsService.Bind(ErrorListModule.KeyErrorListFilterMode, this.WhenValueChanged(x => x.ErrorListFilterMode))
                .Subscribe(x => ErrorListFilterMode = x);
            _settingsService.Bind(ErrorListModule.KeyErrorListShowExternalErrors, this.WhenValueChanged(x => x.ShowExternalErrors))
                .Subscribe(x => ShowExternalErrors = x);

            _dockService.WhenValueChanged(x => x.CurrentDocument).Subscribe(_ => Filter());
        }

        public void RegisterErrorSource(string source)
        {
            ErrorListVisibleSources.Add(source);
        }
        
        private bool Filter(object arg)
        {
            if (arg is not ErrorListItem error) return false;
            var f = FilterMode(error) && FilterErrorSource(error) &&
                   FilterSearchString(error) && (ShowExternalErrors || error.File is IProjectFile || _dockService.OpenFiles.ContainsKey(error.File));

            return f && FilterEnabledType(error);
        }

        private bool FilterMode(ErrorListItem error)
        {
            switch (ErrorListFilterMode)
            {
                case ErrorListFilterMode.All:
                    return true;
                case ErrorListFilterMode.CurrentProject:
                    if (error.File is IProjectFile pf && _projectExplorerExplorerViewModel.ActiveProject == pf.Root) return true;
                    break;
                case ErrorListFilterMode.CurrentFile:
                    if ((_dockService.CurrentDocument as IEditor)?.CurrentFile == error.File) return true;
                    break;
            }
            return false;
        }

        private bool FilterEnabledType(ErrorListItem error)
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

        private bool FilterErrorSource(ErrorListItem error)
        {
            return ErrorListVisibleSource is "All Sources" || ErrorListVisibleSource == error.Source;
        }

        private bool FilterSearchString(ErrorListItem error)
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
        
        public IEnumerable<ErrorListItem> GetErrorsForFile(IFile file)
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
        public void RefreshErrors(IList<ErrorListItem> errors, string source, IFile entry)
        {
            ListEx.RemoveMany(_items, _items.Where(x => (x.File == entry) && x.Source == source && !errors.Contains(x)));
            
            foreach (var e in errors)
            {
                Add(e);
            }
            
            ErrorRefresh?.Invoke(this, entry);
            RefreshCountToggle();
        }

        public void Add(ErrorListItem entry)
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