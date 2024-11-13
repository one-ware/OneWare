using Avalonia.Media;
using DynamicData;
using OneWare.Essentials.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings.ViewModels.SettingTypes;

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
                var subCategoryModel = new SettingsCollectionViewModel(subCategory.Key, subCategory.Value.IconKey);

                foreach (var setting in subCategory.Value.Settings)
                    switch (setting)
                    {
                        case ComboBoxSearchSetting csS:
                            subCategoryModel.SettingModels.Add(new ComboBoxSearchSettingViewModel(csS));
                            break;
                        case ComboBoxSetting cS:
                            subCategoryModel.SettingModels.Add(new ComboBoxSettingViewModel(cS));
                            break;
                        case SliderSetting ss:
                            subCategoryModel.SettingModels.Add(new SliderSettingViewModel(ss));
                            break;
                        case FolderPathSetting pS:
                            subCategoryModel.SettingModels.Add(new PathSettingViewModel(pS));
                            break;
                        case FilePathSetting pS:
                            subCategoryModel.SettingModels.Add(new PathSettingViewModel(pS));
                            break;
                        case PathSetting pS:
                            subCategoryModel.SettingModels.Add(new PathSettingViewModel(pS));
                            break;
                        case CustomSetting cS:
                            subCategoryModel.SettingModels.Add(new CustomSettingViewModel(cS));
                            break;
                        case ListBoxSetting lS:
                            subCategoryModel.SettingModels.Add(new ListBoxSettingViewModel(lS));
                            break;
                        case CheckBoxSetting cbS:
                            subCategoryModel.SettingModels.Add(new CheckBoxSettingViewModel(cbS));
                            break;
                        case TextBoxSetting tS:
                            subCategoryModel.SettingModels.Add(new TextBoxSettingViewModel(tS));
                            break;
                        case ColorSetting cS:
                            subCategoryModel.SettingModels.Add(new ColorPickerSettingViewModel(cS));
                            break;
                    }

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
        _settingsService.Save(_paths.SettingsPath);
    }

    public async Task ResetDialogAsync(FlexibleWindow window)
    {
        var result = await _windowService.ShowYesNoCancelAsync("Warning",
            "Are you sure you want to reset all settings? Paths will not be affected by this!", MessageBoxIcon.Warning, window.Host);

        if (result == MessageBoxStatus.Yes)
            _settingsService.ResetAll();
    }
}