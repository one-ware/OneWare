using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;

namespace OneWare.Essentials.Controls;

public class SearchComboBox : ComboBox
{
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        var searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");

        searchBox!.TextChanged += (sender, args) =>
        {
            SelectedItem = Items.FirstOrDefault(x =>
                x?.ToString()?.StartsWith(searchBox.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase) ?? false);

            searchBox.Focus();
        };


        this.DropDownOpened += (sender, args) =>
        {
            searchBox.Focus();
        };
    }
    
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (SelectedIndex < Items.Count - 1)
                SelectedIndex++;
            e.Handled = true;
            return;
        } 
        if (e.Key == Key.Up)
        {
            if (SelectedIndex > 0)
                SelectedIndex--;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}