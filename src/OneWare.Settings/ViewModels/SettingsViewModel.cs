using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Settings.Models;
using OneWare.Shared.Services;

namespace OneWare.Settings.ViewModels
{
    public class SettingsViewModel : ObservableObject
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
                if (SelectedPage != null && value is SubCategoryModel sub)
                {
                    SelectedPage = SettingPages.FirstOrDefault(x => x.SubCategoryModels.Contains(sub));
                }
                SetProperty(ref _selectedItem, value);
                if(SelectedPage != null) SelectedPage.IsExpanded = true;
            }
        }

        private readonly ISettingsService _settingsService;
        private readonly IPaths _paths;

        public SettingsViewModel(ISettingsService settingsService, IPaths paths)
        {
            _settingsService = settingsService;
            _paths = paths;

            var s = settingsService as SettingsService;
            if (s == null) return;
            
            foreach (var category in s.SettingCategories.OrderBy(x => x.Value.Priority))
            {
                var pageModel = new SettingsPageModel(category.Key, category.Value.IconKey);

                foreach (var subCategory in category.Value.SettingSubCategories.OrderBy(x => x.Value.Priority))
                {
                    var subCategoryModel = new SubCategoryModel(subCategory.Key, subCategory.Value.IconKey);
                    
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
        
        public void Okay(Window window)
        {
            window.Close();
            //Global.SaveSettings();
            _settingsService.Save(Path.Combine(_paths.AppDataDirectory, "settings.json"));
        }

        public void Reset()
        {
            _settingsService.Reset();
        }
    }
}