using Avalonia.Controls;

namespace OneWare.Essentials.Controls;

public class SearchComboBoxItem : ComboBoxItem
{
    private bool _isSearchResult;
    protected override Type StyleKeyOverride => typeof(ComboBoxItem);

    public bool IsSearchResult
    {
        get => _isSearchResult;
        set
        {
            _isSearchResult = value;
            ((IPseudoClasses)Classes).Set(":searchResult", value);
        }
    }
}