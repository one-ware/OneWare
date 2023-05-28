using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Core.LanguageService;
using Prism.Ioc;
using OneWare.Shared.Services;

namespace OneWare.Core.Services
{
    public class Theme : ObservableObject
    {
        public string Name { get;  }
        public IStyle Style { get; }

        public Theme(string name, IStyle style)
        {
            Name = name;
            Style = style;
        }
    }

    public class ThemeManager : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        private readonly Uri _baseUri = new("avares://OneWare.Core/Styles/Themes");

        private readonly Styles _base;

        private string? _appliedTheme;
        private List<Theme> Themes { get; }

        public ThemeManager(ISettingsService settingsService, string? styleOverridePath)
        {
            _settingsService = settingsService;

            _base = new Styles()
            {
                new StyleInclude(_baseUri)
                {
                    Source = new Uri("avares://OneWare.Core/Styles/Themes/BaseTheme.axaml")
                }
            };

            if (styleOverridePath != null)
            {
                _base.Add(new StyleInclude(_baseUri)
                {
                    Source = new Uri(styleOverridePath)
                });
            }
            
            Themes = new List<Theme>
            {
                new("Dark", new Styles
                {
                    new StyleInclude(_baseUri)
                    {
                        Source = new Uri("avares://OneWare.Core/Styles/Themes/DarkTheme.axaml")
                    }
                }),
                new("Light", new Styles
                {
                    new StyleInclude(_baseUri)
                    {
                        Source = new Uri("avares://OneWare.Core/Styles/Themes/LightTheme.axaml")
                    }
                }),
                new("SuperDark", new Styles
                {
                    new StyleInclude(_baseUri)
                    {
                        Source = new Uri("avares://OneWare.Core/Styles/Themes/BlackTheme.axaml")
                    }
                }),
            };

            _settingsService.RegisterTitledCombo("General","Appearance", "General_SelectedTheme", "Theme", "Sets the color scheme for the Application", 
                "Dark", Themes.Select(x => x.Name).ToArray());
        }

        public void Initialize(Application application)
        {
            var themeName = _settingsService.GetSettingValue<string>("General_SelectedTheme");
            var theme = Themes.FirstOrDefault(x => x.Name == themeName) ?? Themes.First();
            
            application.Styles.Insert(0, _base);
            application.Styles.Insert(1, theme.Style);
            application.Styles.Insert(2, (IStyle)AvaloniaXamlLoader.Load(new Uri("avares://OneWare.Core/Styles/Icons.axaml")));
            
            _appliedTheme = themeName;

            _settingsService.GetSettingObservable<string>("General_SelectedTheme").Subscribe(x =>
            {
                if(x != _appliedTheme) ApplyTheme(x);
            });
        }

        private void ApplyTheme(Theme theme)
        {
            if(Application.Current == null) return;
            
            ContainerLocator.Container.Resolve<ILogger>()?.Log($"Apply theme: {theme?.Name}");

            if (theme != null)
            {
                Application.Current.Styles[1] = theme.Style;
                Application.Current.Styles[2] =
                    (IStyle) AvaloniaXamlLoader.Load(new Uri("avares://OneWare.Core/Styles/Icons.axaml"));

                _appliedTheme = theme.Name;
            }

            TypeAssistanceIconStore.Instance.Load();
        }

        private void ApplyTheme(string themeName)
        {
            foreach (var i in Themes)
                if (i.Name == themeName)
                {
                    ApplyTheme(i);
                    return;
                }

            ContainerLocator.Container.Resolve<ILogger>()?.Warning("Theme not found! " + themeName);
        }
    }
}