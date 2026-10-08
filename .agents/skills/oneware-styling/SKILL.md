---
name: oneware-styling
description: Styles OneWare Avalonia UI with the shared design system (tokens, classes, icons, control themes). Use when creating or editing any .axaml view, window, DataTemplate or style in this repo, adding or changing a style class, token or icon under src/OneWare.Essentials/Styles, or fixing visual/theme issues (Light/Dark, accent, spacing, fonts).
---

# OneWare styling

OneWare uses Avalonia **11.3** `SimpleTheme` plus a shared design layer in `src/OneWare.Essentials/Styles/`.
The full token and class reference is [docs/Styling.md](../../../docs/Styling.md). Read the relevant section
before touching a view, and don't invent classes that already exist there.

## Layout of the design layer

| Path | Purpose |
|---|---|
| `Styles/OneWareTheme.axaml` | Entry point. Merges tokens and icons and includes every `Controls/*.axaml` file |
| `Styles/Tokens.axaml`, `TokensDark.axaml`, `TokensLight.axaml` | Colors, radii, spacing, font sizes (shared / per theme variant) |
| `Styles/Icons.axaml` | Monochrome `Icon.*` `StreamGeometry` resources (24×24 frame) |
| `Styles/Controls/<Control>.axaml` | One file per control family (classes and `ControlTheme`s) |
| `Styles/StyleGallery.axaml` | Showcase of every class, opened via Help → Style Gallery in Debug builds |
| `src/OneWare.Core/Styles/Dock.axaml`, `Icons.axaml`, `FileIcons.axaml` | Dock chrome, multi-color `DrawingImage` icons, `FileIcon.*` |

The host includes `OneWareTheme.axaml` once in `src/OneWare.Core/App.axaml`. Plugins and module views must not include it.

## Rules for views

1. Use classes (`card`, `primary`, `muted`, `h3`, `form-row`, …) instead of inline `Background`, `Foreground`,
   `BorderBrush`, `BorderThickness`, `CornerRadius`, `FontSize`, `FontWeight` or `Padding`.
   `Margin`, alignment, sizes and grid layout stay inline.
2. No hex colors in views. Use tokens with `{DynamicResource ...}` (never `StaticResource` for tokens), so
   Light/Dark and accent changes apply live. Colors that come from data are exempt.
3. No local `<UserControl.Styles>` for generic looks. If two views need the same look, add a class to
   `Styles/Controls/` instead.
4. Inside a `DataTemplate`/`ControlTemplate`, an inline `{DynamicResource}` does **not** beat a class style
   (template priority < style trigger). Override with a more specific selector instead.
5. Icons: `<PathIcon Data="{StaticResource Icon.Name}" />` with the `small`/`large`/`muted`/status classes. Code
   that must also run against older `OneWare.Essentials` packages (external plugins) uses `{DynamicResource Icon.Name}`.
6. Don't set a UI `FontFamily` inline. Monospace text uses the `mono` class or `{DynamicResource EditorFont}`.
7. Compiled bindings are on by default (`AvaloniaUseCompiledBindingsByDefault`), so views and `DataTemplate`s
   need `x:DataType`.
8. Tool and document views sit inside dock cards already. Don't add outer borders, corner radii or edge margins.

## Adding or changing a shared style

1. Edit or create the file in `src/OneWare.Essentials/Styles/Controls/`. Keep one control family per file and
   keep the `Design.PreviewWith` block showing every variant.
2. If you create a new file, add a `StyleInclude` for it to `Styles/OneWareTheme.axaml`.
3. New tokens go in `Tokens.axaml` (theme-independent) or in **both** `TokensDark.axaml` and `TokensLight.axaml`.
   Define a `...Color` and a `...Brush` for colors.
4. New icons go in `Styles/Icons.axaml` as `Icon.Name` and must start with the `M0 0L0 0ZM24 24L24 24Z` frame prefix.
5. Add the new class or variant to `Styles/StyleGallery.axaml`.
6. Document it in the matching section of `docs/Styling.md` (class table or bullet list), in the same style as
   the existing entries.
7. Before changing the default look of a control, search `src/` and `studio/` for the control or class name,
   because every module and plugin inherits it.

## Validation

- Build the project that owns the change, for example
  `dotnet build src/OneWare.Essentials/OneWare.Essentials.csproj -v minimal`. XAML errors (bad selectors,
  unknown properties, missing `x:DataType`) only show up at build time.
- Build `studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj` when Core views or the dock styles change.
- For visual checks, run the desktop app in Debug and open Help → Style Gallery in both Light and Dark themes.
