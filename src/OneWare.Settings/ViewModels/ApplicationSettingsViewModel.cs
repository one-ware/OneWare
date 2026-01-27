using DynamicData;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Settings.ViewModels;

public class ApplicationSettingsViewModel : FlexibleWindowViewModelBase
{
    private readonly IPaths _paths;

    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

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