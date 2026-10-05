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
5. **Inside a `DataTemplate`/`ControlTemplate`, a `{DynamicResource}` attribute does not override a class style.**
   Avalonia applies it with `Template` priority, which is lower than a class selector (`StyleTrigger`), so
   `<Border Classes="card" Background="{DynamicResource ...}">` in an item template keeps the card background.
   Override the property with a style instead, for example a `Style Selector="Border.card.my-item"` in the
   parent's `Styles`, or put the brush on a child `Panel` that has no class.

## Tokens

Color keys keep the original [Avalonia SimpleTheme names](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Themes.Simple/Accents/Base.xaml).
Each key exists as a `...Color` and a `...Brush`.

| Key | Use |
|---|---|
| `ThemeBackgroundBrush` | Panel / window background |
| `ThemeControlLowBrush` | Deepest surface: editors, lists, insets |
| `ThemeControlMidBrush` | Input surfaces (TextBox, ComboBox) |
| `ThemeControlMidHighBrush` | Progress track |
| `ThemeButtonSecondaryBrush` / `ThemeButtonSecondaryHoverBrush` | Secondary button fill (opaque and slightly lighter than the background and elevated cards in dark, so it reads raised like a primary button and stays visible over images) |
| `ThemeControlHighBrush` / `ThemeControlVeryHighBrush` | Strong neutral fills / glyphs |
| `ThemeElevatedBrush` | Cards, popups, menus, tooltips |
| `ChatBubbleBrush` | Chat message, tool and reasoning blocks (Brush only; `ThemeControlMidHighColor` in light, `ThemeControlMidColor` in dark) |
| `ThemeControlHighlightMidBrush` / `...HighBrush` / `...LowBrush` | Hover / pressed overlays |
| `ThemeBorderLowBrush` / `...MidBrush` / `...HighBrush` | Subtle / default / strong borders |
| `ThemeForegroundBrush` / `ThemeForegroundLowBrush` | Primary / secondary text |
| `ThemeAccentBrush`, `ThemeAccentBrush2-4`, `ThemeAccentLowBrush` | Accent (100 %, 60 %, 40 %, 20 %, 14 %) |
| `HighlightBrush`, `HighlightForegroundBrush` | Accent fill and the text drawn on it |
| `HighlightForegroundLowBrush`, `HighlightForegroundMidBrush` | Hover / pressed overlay for controls on an accent fill |
| `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `InfoBrush`, `NeutralBrush` | Status colors (`NeutralBrush` is the grey default badge fill) |
| `SuccessLowBrush`, `WarningLowBrush`, `ErrorLowBrush`, `InfoLowBrush` | Tinted status backgrounds |

Non-color tokens:

| Key | Value |
|---|---|
| `ThemeCornerRadiusSmall` / `ThemeCornerRadius` / `ThemeCornerRadiusLarge` / `ThemeCornerRadiusPill` | 3 / 4 / 6 / 999 |
| `ThemeControlHeightSmall` / `ThemeControlHeight` / `ThemeControlHeightLarge` | 20 / 24 / 30 |
| `ThemeIconSizeSmall` / `ThemeIconSize` / `ThemeIconSizeLarge` | 12 / 16 / 20 |
| `ThemeSpacingXSmall` … `ThemeSpacingXLarge` (double) | 2 / 4 / 8 / 12 / 16 |
| `ThemePaddingSmall` / `ThemePadding` / `ThemePaddingLarge` / `ThemePaddingXLarge` (Thickness) | 4 / 8 / 12 / 16 |
| `FontSizeBadge` / `Small` / `Normal` / `Medium` / `Large` / `XLarge` / `XXLarge` | 10 / 11 / 12 / 13 / 16 / 20 / 26 |

Legacy keys such as `GreenAccent` are kept as aliases.

Fonts: the UI uses the platform font (`$Default`, also available as `ContentControlThemeFontFamily`): Segoe UI on
Windows, SF Pro on macOS, and the bundled Inter on Linux and in the browser (set up by `AppBuilder.WithOneWareFonts()`).
Don't set a UI `FontFamily` inline. Monospace text uses `{DynamicResource EditorFont}` (or the `mono` class), which follows
the editor font setting: bundled JetBrains Mono NL (default), Fira Code, Cascadia Mono, or any installed monospace font.

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
- File type icons are `FileIcon.*` `DrawingImage` resources in `OneWare.Core/Styles/FileIcons.axaml`,
  converted from the MIT-licensed [Material Icon Theme](https://github.com/material-extensions/vscode-material-icon-theme)
  for VS Code. Register them for extensions with `IFileIconService.RegisterFileIcon("FileIcon.Json", ".json")`.
  To add one, convert its SVG into a `DrawingGroup` with a transparent frame the size of the SVG `viewBox`, and
  use `F1` geometries, because SVG fills are nonzero.
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
`secondary`, `primary`, `danger` and `ghost` buttons get the input height as a `MinHeight` (`ThemeControlHeight`, or
`ThemeControlHeightSmall` / `ThemeControlHeightLarge` with `small` / `large`) and are centered vertically, so they line up
with text boxes and combo boxes in a row. Larger content (images, multi-line text) grows the button, no `Height="NaN"`
needed. To go below the minimum, set `MinHeight="0"` (or a smaller `MinHeight`) together with `Height`. `icon` buttons are
a fixed square instead, so an inline `Width` / `Height` of any size wins.

### Surfaces (`Border`)

| Class | Look |
|---|---|
| `card` (+ `compact`) | Elevated, bordered, rounded block |
| `panel` | Flat bordered block on the panel background |
| `inset` | Recessed area (lists, previews) |
| `inset-content` | Put directly inside `inset` when the content has its own backgrounds; clips it inside the rounded outline |
| `header` / `footer` | Bar with a bottom / top divider |
| `editor-bar` | Contextual bar above an editor (`EditView_Top` extensions), see below |
| `statusbar` | Accent strip at the bottom of the window; text, icons, buttons and top-level menu items are white in both themes (popups opened from it keep the normal colors) |
| `divider` (+ `vertical`) | 1 px line |
| `overlay` | Floating popup surface with shadow |
| `badge` (+ `accent` `success` `warning` `error`, `small` for dense rows) | Solid status pill (neutral grey by default) with small white text, dark on `success`; put a `TextBlock` inside. Not a container for buttons, use `inset` for that |
| `ItemsControl.tags` | Row of `badge small` pills for `ProjectExplorerTag` items (project explorer file tags) |
| `callout` (+ `success` `warning` `error`) | Inline info box |
| `empty-state` | Centered placeholder |
| `interactive` / `selected` | Hover and selection modifiers for clickable cards |
| `RoundBorder`, `ValidBorder`, `InvalidBorder` | Legacy |

Docking (`OneWare.Core/Styles/Dock.axaml`): every tool and document dock sits in a rounded, clipped card
(`Border.dock-card`, outline drawn on top by `Border.dock-card-outline`), with the 4 px splitters as gaps between
cards. Tool headers have a 25 px surface plus a neutral 1 px separator towards the content (`DockToolChromeHeaderHeight`,
26 px in total) in both states; while a tool is active its header surface and the surrounding outline
(`Border.dock-card-header-outline`) use the accent color.
Document tabs sit above the card on the background; the accent separator forms the card's top edge. Tool and document views therefore need no outer
border or corner radius of their own, and should not add margins to keep content away from the card edge.

### Inputs and lists

- `TextBox`: `small`, `bare` (no border/background), `multiline` (legacy `MultilineTextBox`),
  `search` (compact padding for `InnerLeftContent` / `InnerRightContent` icons)
- `SearchBox` (`OneWare.Essentials.Controls`): the standard search/filter field, a single `TextBox` with a leading
  search icon and a clear button. Set `SearchButtonVisible="False"` for live filtering, and bind `IsBusy` to show a
  spinner. Do not wrap it in extra borders or give it a background. Put it in a strip with `Padding="4"`.
- `ComboBox`: `small`, `ghost` (borderless and transparent like a toolbar button, highlighted on hover / while open)
- `DropDownButton`: styled like a `ComboBox` field (rounded corners, compact chevron, accent border while the flyout
  is open). Use it for "Sort by: …" style pickers that open a `MenuFlyout`.
- `NumericUpDown`: `small`. Same frame and padding as `TextBox`, with a compact up/down column on the right.
- `Slider`: rounded track with an accent-filled range and a ringed thumb. `TickPlacement` / `TickFrequency` / `Ticks`
  draw tick marks. Set `Foreground` (fill) or `Background` (track) to recolor it.
- `ToggleSwitch`: `small` (compact 32×16 track with `Content` as a label to its right, fits a 24 px toolbar). Applied
  automatically inside `StackPanel.toolbar`. `OnContent` / `OffContent` are not shown in this variant.
- `ListBox`: `transparent`, `InvisibleSelection`. By default it is an inset field (control background, low border,
  same corner radius as `TextBox`). Setting `BorderThickness="0"` makes it square for lists that sit edge-to-edge in a panel.
  Items are rounded rows with a hover highlight and an accent selection.
- `Expander`: `card`. By default it is a flat header row with a rotating chevron and a ghost-button hover, with no padding
  around the content. `card` turns it into a bordered section with a divider between header and content. Set
  `HorizontalContentAlignment="Stretch"` to stretch custom header content (e.g. a trailing badge).
- `ProgressBar`: a flat, square-cornered track with an accent fill, 6 px thick by default. Use `large` (12 px) for prominent
  progress such as downloads and installs; set `Height` only when a custom thickness is really needed. Don't set
  `Background` or `CornerRadius`. `ShowProgressText` shows the percentage next to the bar. `fill` stretches the bar over its container, e.g. as a
  progress background behind a button's content. It has a transparent track and a subtle overlay fill, which turns
  light on `primary` / `danger` buttons.
- `TreeDataGrid`: themed app-wide (`Controls/TreeDataGrid.axaml`), so no local theme include is needed. It has compact
  rows with a hover highlight, an accent selection and a 16 px indent per level. The Project Explorer and Problems panels use it.

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
| `editor-bar-items` (`WrapPanel`) | Groups of an editor bar; wraps onto new lines when the editor is narrow |
| `form` | Vertical, 8 px spacing |
| `form-row` | Horizontal, 8 px spacing, centered labels |
| `section` | 4 px spacing |
| `page` | 12 px spacing and 16 px outer margin |
| `spacing-small` / `spacing` / `spacing-large` | 4 / 8 / 12 px spacing |

### Editor bar

Toolbars above an editor (`EditView_Top` extensions, test bench simulator options) share one layout: the primary
action first, then labelled option groups. Groups wrap onto new lines on narrow editors instead of being clipped.
Labels inside `form-row` groups are muted, rarely used options go into a settings flyout (`Button.icon`).

```xml
<Border Classes="editor-bar">
    <WrapPanel Classes="editor-bar-items">
        <Button Classes="primary" Command="{Binding RunCommand}">
            <StackPanel Orientation="Horizontal" Spacing="6">
                <PathIcon Width="10" Height="10" Data="{StaticResource Icon.Play}" />
                <TextBlock Text="Run" VerticalAlignment="Center" />
            </StackPanel>
        </Button>
        <StackPanel Classes="form-row">
            <TextBlock Text="Arguments" />
            <TextBox Width="180" Text="{Binding Arguments}" />
        </StackPanel>
    </WrapPanel>
</Border>
```

Nested extensions (`TestBenchToolbarTopUiExtension`) provide only a `WrapPanel.editor-bar-items` without the outer
border; the host bar already draws it.

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
