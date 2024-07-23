using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace OneWare.Essentials.Controls;

public class SearchComboBox : ComboBox
{
    private TextBox? _searchBox;
    
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox")!;

        _searchBox!.TextChanged += (sender, args) =>
        {
            SelectedItem = Items.FirstOrDefault(x =>
                x?.ToString()?.StartsWith(_searchBox.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase) ?? false);
            
            _searchBox.Focus();
        };
        
        this.DropDownOpened += (sender, args) =>
        {
            _searchBox.Focus();
        };
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (SelectedIndex < Items.Count - 1)
                SelectedIndex++;
            e.Handled = true;

            _searchBox?.Focus();
            return;
        } 
        if (e.Key == Key.Up)
        {
            if (SelectedIndex > 0)
                SelectedIndex--;
            e.Handled = true;
            
            _searchBox?.Focus();
            return;
        }

        base.OnKeyDown(e);
    }
}