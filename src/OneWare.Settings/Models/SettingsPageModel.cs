using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Settings.Models
{
    public class SettingsPageModel : ObservableObject
    {
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        
        private string _header;
        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }
        
        private IImage? _icon;
        public IImage? Icon
        {
            get => _icon;
            private set => SetProperty(ref _icon, value);
        }
        
        private ObservableCollection<SettingsSubCategoryModel> _subCategoryModels = new();
        public ObservableCollection<SettingsSubCategoryModel> SubCategoryModels
        {
            get => _subCategoryModels;
            set => SetProperty(ref _subCategoryModels, value);
        }

        public SettingsPageModel(string title, string? iconKey)
        {
            _header = title;

            if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));
            
            if (iconKey == null) return;

            Application.Current.GetResourceObservable(iconKey).Subscribe(x =>
            {
                Icon = x as IImage;
            });
        }
    }
}