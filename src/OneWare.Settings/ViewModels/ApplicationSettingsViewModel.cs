using DynamicData;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Settings.ViewModels;

public class ApplicationSettingsViewModel : FlexibleWindowViewModelBase
{
    private readonly IPaths _paths;

    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    private string _searchText = string.Empty;

    private object? _selectedItem = new();

    private SettingsPageViewModel? _selectedPage;

    public ApplicationSettingsViewModel(ISettingsService settingsService, IPaths paths, IWindowService windowService)
    {
        Id = "Settings";
        Title = "Settings";

        _settingsService = settingsService;
        _paths = paths;
        _windowService = windowService;

        var s = settingsService as SettingsService;
        if (s == null) return;

        foreach (var category in s.SettingCategories.OrderBy(x => x.Value.Priority))
        {
            var pageModel = new SettingsPageViewModel(category.Key, category.Value.IconKey);

            foreach (var subCategory in category.Value.SettingSubCategories.OrderBy(x => x.Value.Priority))
            {
                var subCategoryModel = new SettingsCollectionViewModel(subCategory.Key, subCategory.Value.IconKey)
                {
                    SettingModels = { subCategory.Value.Settings }
                };

                pageModel.SettingCollections.Add(subCategoryModel);
            }

            SettingPages.Add(pageModel);
        }

        SelectedPage = SettingPages.FirstOrDefault();
    }

    public List<SettingsPageViewModel> SettingPages { get; } = new();

    public SettingsPageViewModel? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value == null) return;
            SetProperty(ref _selectedPage, value);
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            SelectedPage = value as SettingsPageViewModel;
            if (SelectedPage != null && value is SettingsCollectionViewModel sub)
                SelectedPage = SettingPages.FirstOrDefault(x => x.SettingCollections.Contains(sub));
            SetProperty(ref _selectedItem, value);
            if (SelectedPage != null) SelectedPage.IsExpanded = true;
        }
    }

    /// <summary>
    ///     Free-text search applied to category, sub-category and individual setting titles/descriptions.
    ///     An empty value shows the full tree.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        var query = _searchText.Trim();
        var hasQuery = query.Length > 0;

        SettingsPageViewModel? firstMatchingPage = null;

        foreach (var page in SettingPages)
        {
            var pageHeaderMatches = !hasQuery || Matches(page.Header, query);
            var anyChildMatches = false;

            foreach (var collection in page.SettingCollections)
            {
                var collectionHeaderMatches = !hasQuery || Matches(collection.Header, query);
                var anySettingMatches = false;

                foreach (var settingVm in collection.SettingViewModels)
                {
                    var settingMatches = !hasQuery
                                         || pageHeaderMatches
                                         || collectionHeaderMatches
                                         || MatchesSetting(settingVm.Setting, query);
                    settingVm.IsVisibleBySearch = settingMatches;
                    if (settingMatches) anySettingMatches = true;
                }

                var collectionVisible = !hasQuery
                                        || pageHeaderMatches
                                        || collectionHeaderMatches
                                        || anySettingMatches;
                collection.IsVisibleBySearch = collectionVisible;
                if (collectionVisible) anyChildMatches = true;
            }

            var pageVisible = !hasQuery || pageHeaderMatches || anyChildMatches;
            page.IsVisibleBySearch = pageVisible;

            if (hasQuery && pageVisible)
            {
                page.IsExpanded = true;
                firstMatchingPage ??= page;
            }
        }

        if (hasQuery && firstMatchingPage != null && (SelectedPage == null || !SelectedPage.IsVisibleBySearch))
            SelectedItem = firstMatchingPage;
    }

    private static bool Matches(string? value, string query)
    {
        return !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSetting(CollectionSetting setting, string query)
    {
        if (setting is TitledSetting titled)
            return Matches(titled.Title, query) || Matches(titled.HoverDescription, query);
        return false;
    }

    public void Save(FlexibleWindow window)
    {
        Close(window);
        _settingsService.Save(_paths.SettingsPath, false);
    }

    public async Task ResetDialogAsync(FlexibleWindow window)
    {
        var result = await _windowService.ShowYesNoCancelAsync("Warning",
            "Are you sure you want to reset all settings? Paths will not be affected by this!", MessageBoxIcon.Warning,
            window.Host);

        if (result == MessageBoxStatus.Yes)
            _settingsService.ResetAll();
    }
}
