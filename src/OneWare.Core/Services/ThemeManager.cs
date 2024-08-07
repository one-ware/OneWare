﻿using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class ThemeManager : ObservableObject
{
    private readonly IPaths _paths;
    private readonly ISettingsService _settingsService;

    private string? _appliedTheme;

    public ThemeManager(ISettingsService settingsService, IPaths paths)
    {
        _settingsService = settingsService;
        _paths = paths;

        Themes = new List<ThemeVariant>
        {
            new("Dark", null),
            new("Light", null)
        };

        _settingsService.RegisterTitledCombo("General", "Appearance", "General_SelectedTheme", "Theme",
            "Sets the color scheme for the Application",
            Themes[0].Key, Themes.Select(x => x.Key).ToArray());

        //_settingsService.RegisterTitled<Color>("General", "Appearance", "General_SelectedAccentColor", "Accent Color", "Sets the color accent for personalisation", Color.Parse("#FF009688"));

        //_settingsService.GetSettingObservable<Color>("General_SelectedAccentColor").Subscribe(x =>
        //{
        //    if(Application.Current != null) Application.Current.Resources["ThemeAccentColor"] = x;
        //});

        _settingsService.GetSettingObservable<string>("General_SelectedTheme").Subscribe(ApplyTheme);
    }

    private List<ThemeVariant> Themes { get; }

    private void ApplyTheme(string name)
    {
        if (Application.Current == null) throw new NullReferenceException(nameof(Application.Current));

        _appliedTheme = name;
        var theme = Themes.FirstOrDefault(x => (string)x.Key == name) ?? Themes.First();
        Application.Current.RequestedThemeVariant = theme;
    }
}