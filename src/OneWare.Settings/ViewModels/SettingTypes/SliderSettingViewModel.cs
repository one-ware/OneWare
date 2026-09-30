using System.Globalization;
using DynamicData.Binding;
using OneWare.Essentials.Models;

namespace OneWare.Settings.ViewModels.SettingTypes;

public class SliderSettingViewModel : TitledSettingViewModel
{
    // Text as typed by the user; kept while editing so e.g. "60." is not rewritten to "60" (which moves the caret)
    private string? _editText;
    private bool _isUpdatingFromText;

    public SliderSettingViewModel(SliderSetting setting) : base(setting)
    {
        Setting = setting;

        setting.WhenValueChanged(x => x.Value).Subscribe(_ =>
        {
            if (_isUpdatingFromText) return;
            _editText = null;
            OnPropertyChanged(nameof(TextBoxValue));
        });
    }

    public new SliderSetting Setting { get; }

    public string TextBoxValue
    {
        get => _editText ?? FormatValue((double)Setting.Value);
        set
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed))
                throw new FormatException("Please enter a valid number");

            if (parsed < Setting.Min || parsed > Setting.Max)
                throw new ArgumentException($"Value must be between {FormatValue(Setting.Min)} and {FormatValue(Setting.Max)}");

            var rounded = Math.Round(parsed, GetPrecision(Setting.Step));

            _editText = value;
            _isUpdatingFromText = true;
            try
            {
                Setting.Value = rounded;
            }
            finally
            {
                _isUpdatingFromText = false;
            }
        }
    }

    /// <summary>
    ///     Replaces the typed text with the formatted setting value (call when editing ends).
    /// </summary>
    public void CommitText()
    {
        if (_editText == null) return;
        _editText = null;
        OnPropertyChanged(nameof(TextBoxValue));
    }

    private string FormatValue(double value)
    {
        return Math.Round(value, GetPrecision(Setting.Step)).ToString(CultureInfo.CurrentCulture);
    }

    // Helper method: how many decimals do we need?
    private int GetPrecision(double step)
    {
        if (step >= 1)
            return 0;

        var precision = 0;
        while (step < 1)
        {
            step *= 10;
            precision++;
        }

        return precision;
    }
}