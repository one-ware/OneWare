using Avalonia.Controls;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using Autofac;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ListBoxSettingViewModel : TitledSettingViewModel
{
    private int _selectedIndex;

    // Constructor with dependency injection via Autofac
    public ListBoxSettingViewModel(ListBoxSetting setting, IWindowService windowService) : base(setting)
    {
        Setting = setting ?? throw new ArgumentNullException(nameof(setting));
        _windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public new ListBoxSetting Setting { get; }

    private readonly IWindowService _windowService;

    // Add new item logic
    public async Task AddNewAsync(Control owner)
    {
        var result = await _windowService
            .ShowInputAsync("Add", "Add a new value to the list", MessageBoxIcon.Info, null, TopLevel.GetTopLevel(owner) as Window);
        if (!string.IsNullOrWhiteSpace(result)) Setting.Items.Add(result);
    }

    // Edit selected item logic
    public async Task EditSelectedAsync(Control owner)
    {
        if (SelectedIndex < 0 || SelectedIndex >= Setting.Items.Count) return;
        var current = Setting.Items[SelectedIndex];
        var result = await _windowService
            .ShowInputAsync("Edit", "Edit the value", MessageBoxIcon.Info, current, TopLevel.GetTopLevel(owner) as Window);
        if (!string.IsNullOrWhiteSpace(result)) Setting.Items[SelectedIndex] = result;
    }

    // Remove selected item logic
    public void RemoveSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Setting.Items.Count) return;
        Setting.Items.RemoveAt(SelectedIndex);
    }
}
