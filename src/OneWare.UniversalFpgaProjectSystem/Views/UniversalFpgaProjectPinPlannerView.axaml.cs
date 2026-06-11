using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Controls;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectPinPlannerView : FlexibleWindow
{
    private const string PinPropertyColumnTag = "pin-property-column";
    private IDisposable? _pinPropertiesSubscription;

    public UniversalFpgaProjectPinPlannerView()
    {
        InitializeComponent();

        VisiblePinDataGrid.SelectionChanged += (_, _) =>
        {
            if (VisiblePinDataGrid.SelectedItem != null)
                VisiblePinDataGrid.ScrollIntoView(VisiblePinDataGrid.SelectedItem, null);
        };

        ZoomContentControl.WhenValueChanged(x => x.Content).Subscribe(x =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                //ZoomBorder.Fill();
            });
        });

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _pinPropertiesSubscription?.Dispose();
        if (DataContext is UniversalFpgaProjectPinPlannerViewModel vm)
        {
            _pinPropertiesSubscription = vm
                .WhenValueChanged(x => x.ActivePinProperties)
                .Subscribe(props => Dispatcher.UIThread.Post(() => RebuildPinPropertyColumns(props)));
        }
    }

    /// <summary>
    /// Removes any previously generated pin-property columns and inserts a single
    /// flyout column that lets the user edit all declared pin properties per row.
    /// </summary>
    private void RebuildPinPropertyColumns(IReadOnlyList<PinPropertyDefinition>? properties)
    {
        // Remove previously generated property columns
        var toRemove = VisiblePinDataGrid.Columns
            .Where(c => c.Tag is string tag && tag == PinPropertyColumnTag)
            .ToList();
        foreach (var col in toRemove)
            VisiblePinDataGrid.Columns.Remove(col);

        if (properties == null || properties.Count == 0) return;

        // Insert the flyout column before the last column ("Node")
        var insertIdx = Math.Max(0, VisiblePinDataGrid.Columns.Count - 1);
        var flyoutCol = BuildPinPropertiesFlyoutColumn(properties);
        if (insertIdx <= VisiblePinDataGrid.Columns.Count)
            VisiblePinDataGrid.Columns.Insert(insertIdx, flyoutCol);
        else
            VisiblePinDataGrid.Columns.Add(flyoutCol);
    }

    /// <summary>
    /// Builds a single "Properties" DataGrid column. Each cell shows a compact summary
    /// of set property values and opens a flyout with full property editors on click.
    /// </summary>
    private static DataGridColumn BuildPinPropertiesFlyoutColumn(IReadOnlyList<PinPropertyDefinition> properties)
    {
        return new DataGridTemplateColumn
        {
            Header = "Properties",
            Tag = PinPropertyColumnTag,
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<HardwarePinModel>((model, _) =>
            {
                if (model == null) return new Panel();

                // ── Summary text (shown in the cell) ────────────────────────────
                var summaryBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Avalonia.Thickness(4, 0),
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyle.Italic,
                    Text = "—"
                };

                void UpdateSummary()
                {
                    var parts = properties
                        .Select(p => (p.Key, Value: model.GetPinPropertyValue(p.Key)))
                        .Where(x => !string.IsNullOrEmpty(x.Value))
                        .Select(x => $"{x.Key}: {x.Value}");
                    var summary = string.Join("  |  ", parts);
                    summaryBlock.Text = string.IsNullOrEmpty(summary) ? "—" : summary;
                    summaryBlock.FontStyle = string.IsNullOrEmpty(summary) ? FontStyle.Italic : FontStyle.Normal;
                    summaryBlock.Foreground = string.IsNullOrEmpty(summary) ? Brushes.Gray : null;
                }

                UpdateSummary();
                model.PinPropertyChanged += (_, _) => UpdateSummary();

                // ── Flyout content ───────────────────────────────────────────────
                var flyoutPanel = new StackPanel
                {
                    Spacing = 10,
                    Margin = new Avalonia.Thickness(8),
                    MinWidth = 220
                };

                foreach (var def in properties)
                {
                    var row = new StackPanel { Spacing = 3 };
                    row.Children.Add(new TextBlock
                    {
                        Text = def.DisplayName,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        FontSize = 12
                    });

                    if (def.Type == PinPropertyType.ComboBox && def.AllowedValues is { Length: > 0 } allowedValues)
                    {
                        var combo = new ComboBox
                        {
                            ItemsSource = allowedValues,
                            SelectedItem = model.GetPinPropertyValue(def.Key),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };

                        var suppress = false;
                        combo.SelectionChanged += (_, _) =>
                        {
                            if (!suppress && combo.SelectedItem is string v)
                                model.SetPinPropertyValue(def.Key, v);
                        };
                        model.PinPropertyChanged += (_, _) =>
                        {
                            var cur = model.GetPinPropertyValue(def.Key);
                            if (combo.SelectedItem as string != cur)
                            {
                                suppress = true;
                                combo.SelectedItem = string.IsNullOrEmpty(cur) ? null : cur;
                                suppress = false;
                            }
                        };
                        row.Children.Add(combo);
                    }
                    else
                    {
                        var tb = new TextBox
                        {
                            Text = model.GetPinPropertyValue(def.Key),
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };
                        tb.TextChanged += (_, _) => model.SetPinPropertyValue(def.Key, tb.Text);
                        model.PinPropertyChanged += (_, _) =>
                        {
                            var cur = model.GetPinPropertyValue(def.Key);
                            if (tb.Text != cur) tb.Text = cur;
                        };
                        row.Children.Add(tb);
                    }

                    flyoutPanel.Children.Add(row);
                }

                var flyout = new Flyout { Content = flyoutPanel };

                var btn = new Button
                {
                    Content = summaryBlock,
                    Flyout = flyout,
                    Padding = new Avalonia.Thickness(0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Avalonia.Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                return btn;
            }, supportsRecycling: false)
        };
    }
}