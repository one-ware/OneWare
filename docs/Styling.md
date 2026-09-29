# Styling Guide

OneWare uses Avalonia's **SimpleTheme** with a shared design layer on top of it. The design layer lives in
`OneWare.Essentials`, so the host app and every plugin get the same look.

```
src/OneWare.Essentials/Styles/
├── OneWareTheme.axaml      entry point (tokens + all control styles)
├── Tokens.axaml            shared tokens (accent, status, radius, spacing, font sizes)
├── TokensDark.axaml        dark surface/foreground colors
├── TokensLight.axaml       light surface/foreground colors
├── Icons.axaml             monochrome icon geometries (Icon.*)
├── StyleGallery.axaml      showcase of every class (Help → Style Gallery in Debug builds)
└── Controls/               one file per control family
```

The host app includes `OneWareTheme.axaml` once, in `OneWare.Core/App.axaml`. Plugins **do not** need to include it,
because they get it from the host at runtime.

## Rules

1. **Use classes, not inline visuals.** Do not set `Background`, `Foreground`, `BorderBrush`, `BorderThickness`,
   `CornerRadius`, `FontSize`, `FontWeight` or `Padding` inline on views when a class covers it.
   `Margin`, alignment, sizes and grid layout stay inline.
2. **No hex colors in views.** If you need a color, use a token through `{DynamicResource ...}`. Only colors that come
   from data are exempt, for example label colors, waveform colors or overlay colors.
3. **Always use `DynamicResource`** for tokens so Light/Dark switching and accent changes work.
4. **No local `<UserControl.Styles>` for generic looks.** Local styles are fine for behavior that only one view has,
   such as template tweaks or data-driven states. If two views need the same look, add a class to
   `OneWare.Essentials/Styles/Controls` instead.

## Tokens

Color keys keep the original [Avalonia SimpleTheme names](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Themes.Simple/Accents/Base.xaml).
Each key exists as a `...Color` and a `...Brush`.

| Key | Use |
|---|---|
| `ThemeBackgroundBrush` | Panel / window background |
| `ThemeControlLowBrush` | Deepest surface: editors, lists, insets |
| `ThemeControlMidBrush` | Input surfaces (TextBox, ComboBox, secondary buttons) |
| `ThemeControlMidHighBrush` | Hovered secondary button, progress track |
| `ThemeControlHighBrush` / `ThemeControlVeryHighBrush` | Strong neutral fills / glyphs |
| `ThemeElevatedBrush` | Cards, popups, menus, tooltips |
| `ThemeControlHighlightMidBrush` / `...HighBrush` / `...LowBrush` | Hover / pressed overlays |
| `ThemeBorderLowBrush` / `...MidBrush` / `...HighBrush` | Subtle / default / strong borders |
| `ThemeForegroundBrush` / `ThemeForegroundLowBrush` | Primary / secondary text |
| `ThemeAccentBrush`, `ThemeAccentBrush2-4`, `ThemeAccentLowBrush` | Accent (100 %, 60 %, 40 %, 20 %, 14 %) |
| `HighlightBrush`, `HighlightForegroundBrush` | Accent fill and the text drawn on it |
| `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `InfoBrush` | Status colors |
| `SuccessLowBrush`, `WarningLowBrush`, `ErrorLowBrush`, `InfoLowBrush` | Tinted status backgrounds |

Non-color tokens:

| Key | Value |
|---|---|
| `ThemeCornerRadiusSmall` / `ThemeCornerRadius` / `ThemeCornerRadiusLarge` / `ThemeCornerRadiusPill` | 3 / 4 / 6 / 999 |
| `ThemeControlHeightSmall` / `ThemeControlHeight` / `ThemeControlHeightLarge` | 20 / 24 / 30 |
| `ThemeIconSizeSmall` / `ThemeIconSize` / `ThemeIconSizeLarge` | 12 / 16 / 20 |
| `ThemeSpacingXSmall` … `ThemeSpacingXLarge` (double) | 2 / 4 / 8 / 12 / 16 |
| `ThemePaddingSmall` / `ThemePadding` / `ThemePaddingLarge` / `ThemePaddingXLarge` (Thickness) | 4 / 8 / 12 / 16 |
| `FontSizeSmall` / `Normal` / `Medium` / `Large` / `XLarge` / `XXLarge` | 11 / 12 / 13 / 16 / 20 / 26 |

Legacy keys such as `GreenAccent` are kept as aliases.

Inputs (`TextBox`, `ComboBox`, `NumericUpDown`) get a default `Height` of `ThemeControlHeight`. It is a normal
`Height`, not a `MinHeight`, so an inline `Height` on a view still wins. Multiline text boxes reset it to `NaN`.

## Icons

Monochrome icons are `StreamGeometry` resources named `Icon.*` in `OneWare.Essentials/Styles/Icons.axaml`.
Render them with `PathIcon`. They take the `Foreground` of their container, so they turn white on a `primary`
button, follow hover and selection colors, and switch with the theme automatically.

```xml
<PathIcon Data="{StaticResource Icon.Search}" />                     <!-- 16 px -->
<PathIcon Classes="small muted" Data="{StaticResource Icon.Close}" />  <!-- 12 px, secondary color -->
<Button Classes="icon" ToolTip.Tip="Refresh">
    <PathIcon Data="{StaticResource Icon.Refresh}" />
</Button>
```

| `PathIcon` class | Effect |
|---|---|
| `small` / `large` | 12 / 20 px (default 16 px) |
| `muted` | Secondary foreground |
| `accent` `success` `warning` `error` | Colored icon |

- All geometries are normalized to a 24×24 frame with consistent padding, so any size works.
- Help → Style Gallery (Debug builds) lists every icon with its name.
- Plugins that compile against an older `OneWare.Essentials` package should use `{DynamicResource Icon.X}`.
  `StaticResource` fails when the key is not in the referenced package.
- Multi-color icons, such as file types, debugger actions and status badges, stay `DrawingImage` resources in
  `OneWare.Core/Styles/Icons.axaml` and are used with `<Image Source="{DynamicResource ...}" />`.
- The old single-color `DrawingImage` keys (for example `BoxIcons.RegularSearch` and `Material.SettingsOutline`)
  still exist as thin wrappers around the `Icon.*` geometries. They keep `IconModel` and C# code working, but new
  views should use `PathIcon`.
- To add an icon, add an `Icon.Name` geometry to `Icons.axaml`. The geometry must include the
  `M0 0L0 0ZM24 24L24 24Z` frame prefix.

## Classes

### Text (`TextBlock`, `SelectableTextBlock`)

| Class | Look |
|---|---|
| `h1` `h2` `h3` `h4` | Headings (26 / 20 / 16 / 13, SemiBold) |
| `overline` | Small SemiBold secondary section label |
| `muted` | Secondary foreground |
| `caption` | Small + secondary foreground |
| `mono` | Editor font |
| `semibold` `bold` | Font weight |
| `accent` `success` `warning` `error` | Colored text |

### Buttons (`Button`, `ToggleButton`)

| Class | Look |
|---|---|
| *(none)* / `ghost` | Transparent toolbar button with hover highlight |
| `secondary` | Bordered neutral button (legacy: `RoundButton`, `WindowButton`, `SecondaryButton`) |
| `primary` | Solid accent button (legacy: `PrimaryButton`) |
| `danger` | Solid error button |
| `link` | Text-only accent button |
| `icon` | Square icon button, combine with the above |
| `small` / `large` | Size modifiers |

Checked `ToggleButton`s get an accent tint automatically. Use `<StackPanel Classes="WindowButtons">` for dialog button rows.

### Surfaces (`Border`)

| Class | Look |
|---|---|
| `card` (+ `compact`) | Elevated, bordered, rounded block |
| `panel` | Flat bordered block on the panel background |
| `inset` | Recessed area (lists, previews) |
| `header` / `footer` | Bar with a bottom / top divider |
| `divider` (+ `vertical`) | 1 px line |
| `overlay` | Floating popup surface with shadow |
| `badge` (+ `accent` `success` `warning` `error`) | Pill label; put a `TextBlock` inside |
| `callout` (+ `success` `warning` `error`) | Inline info box |
| `empty-state` | Centered placeholder |
| `interactive` / `selected` | Hover and selection modifiers for clickable cards |
| `RoundBorder`, `ValidBorder`, `InvalidBorder` | Legacy |

### Inputs and lists

- `TextBox`: `small`, `bare` (no border/background), `multiline` (legacy `MultilineTextBox`),
  `search` (compact padding for `InnerLeftContent` / `InnerRightContent` icons)
- `SearchBox` (`OneWare.Essentials.Controls`): the standard search/filter field, a single `TextBox` with a leading
  search icon and a clear button. Set `SearchButtonVisible="False"` for live filtering, and bind `IsBusy` to show a
  spinner. Do not wrap it in extra borders or give it a background. Put it in a strip with `Padding="4"`.
- `ComboBox`: `small`
- `Slider`: rounded track with an accent-filled range and a ringed thumb. `TickPlacement` / `TickFrequency` / `Ticks`
  draw tick marks. Set `Foreground` (fill) or `Background` (track) to recolor it.
- `ListBox`: `transparent`, `InvisibleSelection`

### Flyouts

Every `Flyout` is rounded and elevated, with 8 px padding. Do not set `FlyoutPresenterTheme`, and do not add an outer
margin or background to the content. Choose a variant with `FlyoutPresenterClasses`:

| Class | Use |
|---|---|
| (none) | Text, help/markdown, and small forms |
| `list` | `ListBox` / `MenuItem` pickers: 4 px padding like menus, and the `ListBox` becomes transparent |
| `flush` | Content that brings its own layout (headers, color picker); no padding, clipped to the rounded corners |

```xml
<Flyout FlyoutPresenterClasses="list">
    <ListBox ItemsSource="{Binding Items}" />
</Flyout>
```

`FlyoutNoPadding` is a legacy alias for the default flyout.

### Layout (`StackPanel`)

| Class | Look |
|---|---|
| `toolbar` (legacy `ToolBar`) | Horizontal, 24 px high, 4 px spacing |
| `form` | Vertical, 8 px spacing |
| `form-row` | Horizontal, 8 px spacing, centered labels |
| `section` | 4 px spacing |
| `page` | 12 px spacing and 16 px outer margin |
| `spacing-small` / `spacing` / `spacing-large` | 4 / 8 / 12 px spacing |

## Example

```xml
<Border Classes="card">
    <StackPanel Classes="section">
        <DockPanel>
            <Border Classes="badge success" DockPanel.Dock="Right"><TextBlock Text="Ready" /></Border>
            <TextBlock Classes="h4" Text="{Binding Name}" />
        </DockPanel>
        <TextBlock Classes="muted" Text="{Binding Description}" TextWrapping="Wrap" />
        <StackPanel Classes="form-row" HorizontalAlignment="Right">
            <Button Classes="secondary" Content="Details" />
            <Button Classes="primary" Content="Run" />
        </StackPanel>
    </StackPanel>
</Border>
```
