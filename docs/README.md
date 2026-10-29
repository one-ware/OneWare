# OneWare Studio Developer Documentation

This is the developer wiki for OneWare Studio: how the code base is organized, how to run and debug it, and
where to find more detailed guides. For how to submit changes, see [CONTRIBUTING.md](../CONTRIBUTING.md).
End-user documentation lives at [one-ware.com/docs](https://one-ware.com/docs/studio/setup).

| Guide | Contents |
|---|---|
| [Plugin Development](PluginDevelopment.md) | Module lifecycle, packaging plugins, `OneWare.Essentials` service reference, Universal FPGA project system, AI agents and skills |
| [Styling](Styling.md) | Design tokens, style classes, icons and UI rules for `.axaml` views |
| [Source Control](SourceControl.md) | Git integration internals |
| [Changelog](changelog.md) | User-facing release notes |

## Tech stack

- **.NET 10**, C# with nullable reference types
- **[Avalonia 11.3](https://docs.avaloniaui.net/)** with `SimpleTheme` and a shared design layer
  ([Styling](Styling.md)), [Dock.Avalonia](https://github.com/wieslawsoltes/Dock) for the docking layout and
  [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) + TextMate for the editor
- **MVVM** with [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
  (`ObservableObject`, `SetProperty`, `RelayCommand`), compiled bindings enabled by default
- **Dependency injection** with `Microsoft.Extensions.DependencyInjection`
- **Logging** with `Microsoft.Extensions.Logging` backed by Serilog
- **Language servers** over LSP (OmniSharp client) for VHDL, Verilog, C++, Python, TypeScript and more
- **Tests** with xUnit and `Avalonia.Headless.XUnit`

## Repository layout

```
OneWare.slnx              solution (open this in your IDE)
build/props/              one .props file per NuGet package (central versions), Base.props (version), OneWare.Module.props
src/                      libraries and feature modules
  OneWare.Essentials/     public SDK for plugins: service interfaces, models, controls, design system (NuGet package)
  OneWare.Core/           app shell: App.axaml, DI bootstrap, main window, docking, built-in services
  OneWare.<Feature>/      feature modules (language support, tools, panels, integrations)
  VtNetCore.Avalonia/     git submodule, terminal emulator used by OneWare.Terminal
studio/
  OneWare.Studio/         Studio app shared by desktop and browser (Studio modules, assets)
  OneWare.Studio.Desktop/ desktop entry point, CLI options, desktop-only modules, packaging metadata
  OneWare.Studio.Browser/ WebAssembly entry point
  *Installer/             Windows and macOS installer projects
demo/                     minimal app built on OneWare.Core, useful as a reference for custom IDEs
tests/                    unit and headless UI tests, plus OneWare.TestPlugin
docs/                     this documentation
```

## Architecture

### Application layers

The app is composed in three layers. Each layer derives from the one below and adds modules in
`ConfigureModuleCatalog`:

| Layer | Class | Adds |
|---|---|---|
| Core | `OneWare.Core.App` (`src/OneWare.Core/App.axaml.cs`) | Shell, core services, built-in panels (project explorer, search, errors, output, JSON/TOML, …) |
| Studio | `StudioApp` (`studio/OneWare.Studio/StudioApp.cs`) | Universal FPGA project system, waveform viewer, Markdown, adapters; runs on desktop and in the browser |
| Desktop | `DesktopStudioApp` (`studio/OneWare.Studio.Desktop/DesktopStudioApp.cs`) | Package manager, updater, terminals, source control, language/toolchain integrations, chat and AI, plugin loading |

The browser build shares `StudioApp`, so code in the Core and Studio layers must not start processes or rely on
native libraries.

### Modules

Every feature is a module: a class deriving from `OneWareModuleBase` (`IOneWareModule`) with two phases:

1. `RegisterServices(IServiceCollection)`: register the module's own services.
2. `Initialize(IServiceProvider)`: resolve host services and register extensions such as menu items, dock
   panels, languages, file icons, project types, toolchains or settings.

Built-in modules and external plugins use the same contract. Plugins are folders under
`~/OneWareStudio/Packages/Plugins` that contain the assemblies and a `compatibility.txt`, and are usually
installed through the package manager. See [Plugin Development](PluginDevelopment.md) for details and the
list of available services.

### Views and view models

- View models derive from `ObservableObject` (or a OneWare base type in `OneWare.Essentials.ViewModels`).
- `ViewLocator` (`src/OneWare.Core/ViewLocator.cs`) maps `Foo.ViewModels.BarViewModel` to `Foo.Views.BarView` and
  resolves the view from the DI container, so keep that naming.
- Dockable tools and documents are registered through `IMainDockService`. Their views are shown inside rounded dock
  cards and don't need their own outer border.
- All UI follows the shared design system in `OneWare.Essentials/Styles`. See [Styling](Styling.md).

### Projects and languages

- Project types (`OneWare.FolderProjectSystem`, `OneWare.UniversalFpgaProjectSystem`) plug into
  `IProjectManagerService` and the project explorer.
- Editors get language features from `ILanguageManager`: TextMate grammars for highlighting and
  `TypeAssistance` / language services (usually LSP servers) for completion, diagnostics and formatting.
- External binaries such as language servers and toolchains are installed through the package manager
  (`IPackageService`) and started through `IChildProcessService` / `IToolService`.

### Language protocol dependencies

The LSP client uses the [OneWare OmniSharp fork](https://github.com/hendrikmennen/csharp-language-server-protocol).
Starting with package version 1.0.2, its JSON-RPC dispatcher and contracts are owned by the fork; it no longer
depends on MediatR. Keep the client and protocol versions aligned in
`build/props/OmniSharp.Extensions.Language.props`.

Plugins using the language protocol packages must be rebuilt against 1.0.2 or later: the request, handler
and pipeline contract identities changed. Custom handlers should import
`OmniSharp.Extensions.JsonRpc.Dispatching` instead of `MediatR`. This is a binary compatibility break in the
protocol dependency, even though OneWare's own service signatures are unchanged.

When validating an unpublished fork version, pack it in the fork checkout and restore OneWare with
`dotnet restore OneWare.slnx --source <local-package-directory> --source https://api.nuget.org/v3/index.json`.
Publish that version of the fork packages before merging the corresponding OneWare dependency update so
ordinary restores and CI can resolve it.

## Running and debugging

```bash
dotnet run --project studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj
```

Debug builds add developer tooling:

- **Avalonia DevTools**: press `F12` in any OneWare window to inspect the visual tree, styles and bindings.
- **Help → Style Gallery**: every style class, control variant and icon, useful for checking Light/Dark themes.

### Command-line options

| Option | Environment variable | Purpose |
|---|---|---|
| `<path or url>` | `ONEWARE_OPEN_PATH` / `ONEWARE_OPEN_URL` | Open a file, folder or `oneware://` URL at startup |
| `--oneware-dir` | `ONEWARE_DIR` | Documents directory (default `~/OneWareStudio`) |
| `--oneware-projects-dir` | `ONEWARE_PROJECTS_DIR` | Default projects directory |
| `--oneware-appdata-dir` | `ONEWARE_APPDATA_DIR` | Application data directory (settings, layouts) |
| `--modules` | `ONEWARE_MODULES` | Load a plugin folder at startup, handy while developing a plugin |
| `--autolaunch` | `ONEWARE_AUTOLAUNCH` | Run an action registered by a plugin after startup |
| `--package-repository` | `ONEWARE_PACKAGE_REPOSITORY` | Override the package repository URL(s), separated by `;` |
| `--configuration-profile` | `ONEWARE_CONFIGURATION_PROFILE` | Apply a configuration profile (see the [README](../README.md#configuration-profiles)) |
| `-C`, `--working-directory` | – | Change the working directory before anything else runs |

Using a separate `--oneware-dir` and `--oneware-appdata-dir` gives you a clean, isolated profile for testing
without touching your normal installation. `./run-fresh.sh` does this for you with a new temporary profile (pass
OneWare Studio arguments after `--`, see `./run-fresh.sh --help`).

### CLI commands

The Studio executable also hosts headless commands (`stop`, `agent init`, `extensions …`, `cloud login|logout`, plus any
`IOneWareCliModule` from installed plugins). `studio/OneWare.Studio.Desktop/Cli/StudioCli.cs` decides before
Avalonia starts: if the first positional argument looks like a command name (letters, digits, `-`, `_`) and is
not an existing file or folder, or `--help`/`--version` is passed, the CLI runs and the GUI is never started.
Anything else (no arguments, a path or a `oneware://` URL) starts the GUI as before.

`oneware agent init` installs the built-in `oneware-cli` skill and synchronizes skills explicitly
contributed by installed extensions for external coding agents into `~/.agents/skills/`. It copies the
complete skill directory, including supporting files, and records ownership and file hashes in
`oneware-skills.json` in the OneWare documents directory. Unchanged
skills from uninstalled extensions and omitted `--modules` directories are removed on the next run.
Existing unmanaged or locally modified skills are preserved and reported as conflicts. A missing package
database prevents cleanup of package extension skills; an unreadable database or failed extension
discovery prevents synchronization. The command does not export the Studio chat's own skills or functions.

On Windows, `OneWareStudio.exe` is a GUI-subsystem app, so the publish step also creates `oneware.exe`, a copy
with the console subsystem. Use it from terminals; without a command it starts `OneWareStudio.exe` detached.

### Single instance and IPC

Only one Studio runs per user. `StudioInstanceChannel` (`src/OneWare.Essentials/Helpers`) holds an exclusive lock
file and a named pipe (a Unix socket on Linux/macOS), both scoped to the current user: `%TEMP%\OneWare` on Windows,
`$XDG_RUNTIME_DIR/oneware` (or `/tmp/oneware-<user>`) on Linux and `$TMPDIR/OneWare` on macOS. A second start
forwards its path or `oneware://` URL to the running instance and exits. CLI commands use the same channel to send
commands (`cmd:<name>`), for example `oneware stop` sends `shutdown` and waits for the lock to be released.
Messages that arrive while Studio is still starting are queued until startup content has loaded.

### Where things are stored

| What | Default location |
|---|---|
| Logs (one file per day, 7 days kept) | `~/OneWareStudio/Logs` |
| Packages, plugins, native tools | `~/OneWareStudio/Packages` |
| Projects | `~/OneWareStudio/Projects` |
| Settings and layouts | `%APPDATA%\OneWareStudio` (Windows), `~/.config/OneWareStudio` (Linux), `~/Library/Application Support/OneWareStudio` (macOS) |

## Building, testing and packaging

```bash
dotnet workload restore
dotnet build OneWare.slnx
dotnet test OneWare.slnx
```

- CI (`.github/workflows/test.yml`) builds the solution and runs all tests on every pull request.
- Package versions are managed centrally in `build/props/` and updated by Dependabot.
- Releases are cut by maintainers: `StudioVersion` in `build/props/Base.props`, the release entry in
  `studio/OneWare.Studio.Desktop/com.one_ware.OneWare.metainfo.xml` and `docs/changelog.md` are updated
  together, then the `Publish Studio Desktop` workflow builds Windows, macOS, Linux (Snap, Flathub) and web
  releases and publishes the NuGet packages.

## AI coding agents

[AGENTS.md](../AGENTS.md) and the skills in `.agents/skills/` describe the repository conventions for AI
coding agents (GitHub Copilot, Codex and others). Update them when the conventions change.
