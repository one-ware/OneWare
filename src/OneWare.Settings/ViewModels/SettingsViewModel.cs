using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Settings.Models;
using OneWare.Shared;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using OneWare.Shared.ViewModels;

namespace OneWare.Settings.ViewModels
{
    public class SettingsViewModel : FlexibleWindowViewModelBase
    {
        public List<SettingsPageModel> SettingPages { get; } = new();

        private SettingsPageModel? _selectedPage;
        public SettingsPageModel? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (value == null) return;
                SetProperty(ref _selectedPage, value);
            }
        }

        private object? _selectedItem = new();
        public object? SelectedItem
        {
            get => _selectedItem;
            set
            {
                SelectedPage = value as SettingsPageModel;
                if (SelectedPage != null && value is SettingsSubCategoryModel sub)
                {
                    SelectedPage = SettingPages.FirstOrDefault(x => x.SubCategoryModels.Contains(sub));
                }
                SetProperty(ref _selectedItem, value);
                if(SelectedPage != null) SelectedPage.IsExpanded = true;
            }
        }

        private readonly ISettingsService _settingsService;
        private readonly IPaths _paths;
        private readonly IWindowService _windowService;

        public SettingsViewModel(ISettingsService settingsService, IPaths paths, IWindowService windowService)
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
                var pageModel = new SettingsPageModel(category.Key, category.Value.IconKey);

                foreach (var subCategory in category.Value.SettingSubCategories.OrderBy(x => x.Value.Priority))
                {
                    var subCategoryModel = new SettingsSubCategoryModel(subCategory.Key, subCategory.Value.IconKey);
                    
                    foreach (var setting in subCategory.Value.Settings)
                    {
                        if (setting is ComboBoxSetting cS)
                        {
                            subCategoryModel.SettingModels.Add(new SettingModelComboBox(cS));
                        }
                        else
                        {
                            switch (setting.Value)
                            {
                                case bool:
                                    subCategoryModel.SettingModels.Add(new SettingModelCheckBox(setting));
                                    break;
                                case string:
                                case int:
                                case float:
                                case double:
                                    subCategoryModel.SettingModels.Add(new SettingModelTextBox(setting));
                                    break;
                                case Color:
                                    subCategoryModel.SettingModels.Add(new SettingModelColorPicker(setting));
                                    break;
                            }
                        }
                    }
                    
                    pageModel.SubCategoryModels.Add(subCategoryModel);
                }

                SettingPages.Add(pageModel);
            }

            SelectedPage = SettingPages.FirstOrDefault();
        }
        
        public void Okay(FlexibleWindow window)
        {
            this.Close(window);
            _settingsService.Save(Path.Combine(_paths.AppDataDirectory, "settings.json"));
        }

        public async Task ResetDialogAsync()
        {
            var result = await _windowService.ShowYesNoCancelAsync("Warning",
                "Are you sure you want to reset all settings? Paths will not be affected by this!", MessageBoxIcon.Warning);
            
            if(result == MessageBoxStatus.Yes)
                _settingsService.Reset();
        }
    }
}