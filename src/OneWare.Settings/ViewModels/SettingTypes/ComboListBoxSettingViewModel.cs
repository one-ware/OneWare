using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class ComboListBoxSettingViewModel : TitledSettingViewModel
{
    private int _selectedListIndex = -1;
    private string? _selectedOption;

    public ComboListBoxSettingViewModel(ComboListBoxSetting setting) : base(setting)
    {
        Setting = setting;
        SelectedOption = setting.Options.FirstOrDefault();
    }

    public new ComboListBoxSetting Setting { get; }

    /// <summary>The item currently highlighted in the list of added entries.</summary>
    public int SelectedListIndex
    {
        get => _selectedListIndex;
        set => SetProperty(ref _selectedListIndex, value);
    }

    /// <summary>The option currently selected in the predefined-options dropdown.</summary>
    public string? SelectedOption
    {
        get => _selectedOption;
        set => SetProperty(ref _selectedOption, value);
    }

    /// <summary>Appends <see cref="SelectedOption"/> to the items list.</summary>
    public void AddSelected()
    {
        if (!string.IsNullOrWhiteSpace(SelectedOption))
            Setting.Items.Add(SelectedOption);
    }

    /// <summary>Removes the item at <see cref="SelectedListIndex"/> from the items list.</summary>
    public void RemoveSelected()
    {
        if (SelectedListIndex < 0 || SelectedListIndex >= Setting.Items.Count) return;
        Setting.Items.RemoveAt(SelectedListIndex);
    }

    /// <summary>Moves the selected item one position up in the list.</summary>
    public void MoveUp()
    {
        if (SelectedListIndex <= 0 || SelectedListIndex >= Setting.Items.Count) return;
        Setting.Items.Move(SelectedListIndex, SelectedListIndex - 1);
        SelectedListIndex--;
    }

    /// <summary>Moves the selected item one position down in the list.</summary>
    public void MoveDown()
    {
        if (SelectedListIndex < 0 || SelectedListIndex >= Setting.Items.Count - 1) return;
        Setting.Items.Move(SelectedListIndex, SelectedListIndex + 1);
        SelectedListIndex++;
    }
}

