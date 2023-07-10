using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Shared.Services;

namespace OneWare.Core.Services
{
    public class ThemeManager : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IPaths _paths;

        private string? _appliedTheme;
        private List<ThemeVariant> Themes { get; }

        public ThemeManager(ISettingsService settingsService, IPaths paths)
        {
            _settingsService = settingsService;
            _paths = paths;

            Themes = new List<ThemeVariant>
            {
                new ThemeVariant("Dark", null),
                new ThemeVariant("Light", null),
            };

            _settingsService.RegisterTitledCombo("General","Appearance", "General_SelectedTheme", "Theme", "Sets the color scheme for the Application", 
               Themes[0].Key, Themes.Select(x => x.Key).ToArray());
            
            _settingsService.RegisterTitled("General", "Appearance", "General_SelectedAccentColor", "Accent Color", "Sets the color accent for personalisation", Color.Parse("#FFFFFF"));
            
            _settingsService.Load(paths.SettingsPath);
            
            _settingsService.GetSettingObservable<string>("General_SelectedTheme").Subscribe(ApplyTheme);
        }

        private void ApplyTheme(string name)
        {
            if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));
            
            _appliedTheme = name;
            var theme = Themes.FirstOrDefault(x => (string)x.Key == name) ?? Themes.First();
            Application.Current.RequestedThemeVariant = theme;
        }
    }
}