using Avalonia.Controls;

namespace OneWare.Essentials.Controls;

public class SearchComboBoxItem : ComboBoxItem
{
    protected override Type StyleKeyOverride => typeof(ComboBoxItem);

    private bool _isSearchResult;
    
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