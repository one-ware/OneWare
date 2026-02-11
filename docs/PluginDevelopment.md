# OneWare Plugin Developer Guide

This document explains the plugin architecture, the OneWare.Essentials interfaces you can depend on,
and how UniversalFpgaProject support is wired so you can extend it safely.

## Quick start

1) Create a class library that references `OneWare.Essentials`.
2) Implement a module by deriving from `OneWare.Essentials.Services.OneWareModuleBase`.
3) Add a `compatibility.txt` file next to your plugin assemblies.
4) Package the plugin as a folder (assemblies + `compatibility.txt`) and install it via the plugin manager.

Minimal module example:

```csharp
using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace MyCompany.MyPlugin;

public sealed class MyPluginModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        // Optional: register your own services here.
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IProjectExplorerService>()
            .RegisterConstructContextMenu((selected, items) =>
            {
                if (selected is [IProjectFile])
                    items.Add(new MenuItemModel("MyAction") { Header = "My Action" });
            });
    }
}
```

Minimal `compatibility.txt` (one dependency per line):

```
OneWare.Essentials : 1.0.0
```

## Plugin packaging and loading

- Plugins are loaded through `IPluginService.AddPlugin(path)`, which copies the plugin folder into a
  session plugin directory, then loads all `*.dll` files in that folder.
- A plugin must include a `compatibility.txt` file at its root. It lists assembly dependencies and
  versions using `AssemblyName : Version` lines. The check is enforced by
  `OneWare.Essentials.PackageManager.Compatibility.PluginCompatibilityChecker`.
- Modules are discovered by scanning assemblies for `IOneWareModule` implementations.
- If `IOneWareModule.RegisterServices` adds services, they are injected into the main container
  before module initialization. If the app is already running, modules are initialized immediately.

## Module lifecycle and dependency injection

`IOneWareModule` is the entry point for plugins:

- `RegisterServices(IServiceCollection services)` lets you register your own services.
- `Initialize(IServiceProvider serviceProvider)` runs after services are registered.
- `Dependencies` allows declaring other module IDs to load before yours.

Use `serviceProvider.Resolve<T>()` or `ContainerLocator.Current` to resolve OneWare services.

## OneWare.Essentials interfaces

Below is a concise guide to the public interfaces in `OneWare.Essentials`. Use them as stable
integration points.

### Models

- `IPlugin` (src/OneWare.Essentials/Models/IPlugin.cs): plugin descriptor with `Id`, `Path`,
  and compatibility status.
- `IApplicationCommand` (src/OneWare.Essentials/Models/IApplicationCommand.cs): defines a named
  command that can bind to key gestures and execute against an Avalonia `ILogical` source.
- `IProjectExplorerNode` (src/OneWare.Essentials/Models/IProjectExplorerNode.cs): base UI node for
  the project explorer tree (header, icon, children, visual state).
- `IProjectEntry` (src/OneWare.Essentials/Models/IProjectEntry.cs): common file/folder entry data,
  including `Root`, `TopFolder`, `RelativePath`, and `FullPath`.
- `IProjectFile` / `IProjectFolder` (src/OneWare.Essentials/Models/IProjectFile.cs,
  src/OneWare.Essentials/Models/IProjectFolder.cs): file or folder entry operations
  like `AddFile`, `GetEntry`, `GetFiles`, and `GetDirectories`.
- `IProjectRoot` (src/OneWare.Essentials/Models/IProjectRoot.cs): root folder for a project,
  tracks `ProjectTypeId`, `ProjectPath`, and include/exclude logic.
- `IProjectRootWithFile` (src/OneWare.Essentials/Models/IProjectRootWithFile.cs): adds a project
  file path, `UniversalProjectProperties`, and modification tracking.
- `IPackageState` (src/OneWare.Essentials/Models/IPackageState.cs): package status for the package
  manager UI, including progress and installed version data.

### Services

- `IOneWareModule` (src/OneWare.Essentials/Services/IOneWareModule.cs): module lifecycle interface.
- `IPluginService` (src/OneWare.Essentials/Services/IPluginService.cs): install/remove plugins.
- `IPaths` (src/OneWare.Essentials/Services/IPaths.cs): app data paths (projects, packages, plugins,
  settings, temp, etc.). Use this instead of hard-coded paths.
- `IWindowService` (src/OneWare.Essentials/Services/IWindowService.cs): main UI integration
  (menus, UI extensions, dialogs, notifications). Menu paths are string keys such as
  `MainWindow_MainMenu/FPGA`.
- `IMainDockService` (src/OneWare.Essentials/Services/IMainDockService.cs): document/tool docking,
  open files, layout management, and document views.
- `IProjectExplorerService` (src/OneWare.Essentials/Services/IProjectExplorerService.cs): manage
  project roots, load/save, project dialogs, selection, and context menus.
- `IProjectManagerService` (src/OneWare.Essentials/Services/IProjectManagerService.cs): register
  project managers by project type ID or file extension.
- `IProjectSettingsService` (src/OneWare.Essentials/Services/IProjectSettingsService.cs): register
  custom project settings and categories for project settings UI.
- `ISettingsService` (src/OneWare.Essentials/Services/ISettingsService.cs): settings registry,
  storage, binding, and default values for app settings.
- `IApplicationCommandService` (src/OneWare.Essentials/Services/IApplicationCommandService.cs):
  register commands and persist key bindings.
- `ILanguageManager` (src/OneWare.Essentials/Services/ILanguageManager.cs): language support
  registration (TextMate grammars, LSP services, and file extension mapping).
- `IErrorService` (src/OneWare.Essentials/Services/IErrorService.cs): error list UI (register
  sources and publish diagnostics).
- `IOutputService` (src/OneWare.Essentials/Services/IOutputService.cs): output panel writes with
  optional color and project ownership.
- `IFileIconService` (src/OneWare.Essentials/Services/IFileIconService.cs): register per-extension
  file icons (resources or `IImage`).
- `IChildProcessService` (src/OneWare.Essentials/Services/IChildProcessService.cs): spawn and track
  external processes; `ExecuteShellAsync` runs tools with status reporting.
- `ITerminalManagerService` (src/OneWare.Essentials/Services/ITerminalManagerService.cs): execute
  commands in the terminal UI.
- `IToolService` (src/OneWare.Essentials/Services/IToolService.cs): register tool configurations
  and multiple execution strategies per tool.
- `IToolExecutionStrategy` / `IToolExecutionDispatcherService`
  (src/OneWare.Essentials/Services/IToolExecutionStrategy.cs,
  src/OneWare.Essentials/Services/IToolExecutionDispatcherService.cs): plug in custom tool runners.
- `IPackageService` / `IPackageWindowService` (src/OneWare.Essentials/Services/IPackageService.cs,
  src/OneWare.Essentials/Services/IPackageWindowService.cs): manage package repositories and the
  extension manager UI.
- `IApplicationStateService` (src/OneWare.Essentials/Services/IApplicationStateService.cs):
  application state, notifications, and shutdown hooks.
- `IEnvironmentService` (src/OneWare.Essentials/Services/IEnvironmentService.cs): modify
  environment variables and PATH.
- `IHttpService` (src/OneWare.Essentials/Services/IHttpService.cs): download helper for files,
  archives, images, and text.
- `IChatService` / `IChatManagerService` (src/OneWare.Essentials/Services/IChatService.cs,
  src/OneWare.Essentials/Services/IChatManagerService.cs): optional chat integration.
- `IAiFunctionProvider` (src/OneWare.Essentials/Services/IAiFunctionProvider.cs): register AI tools.
- `ISerialMonitorService` (src/OneWare.Essentials/Services/ISerialMonitorService.cs): dockable
  serial monitor tool.
- `IWelcomeScreenService` (src/OneWare.Essentials/Services/IWelcomeScreenService.cs): add items to
  the welcome screen.
- `IWelcomeScreenItem` (src/OneWare.Essentials/Services/IWelcomeScreenService.cs): model for
  custom welcome screen items.
- `ICompositeServiceProvider` (src/OneWare.Essentials/Services/ICompositeServiceProvider.cs):
  combined service provider used to resolve app and plugin services.

### View models and editor integration

- `IExtendedDocument` (src/OneWare.Essentials/ViewModels/IExtendedDocument.cs): dockable documents
  with save/close lifecycle and diagnostic navigation.
- `IExtendedTool` (src/OneWare.Essentials/ViewModels/IExtendedTool.cs): dockable tools with an icon.
- `IEditor` (src/OneWare.Essentials/ViewModels/IEditor.cs): editor documents with access to
  `ExtendedTextEditor` and `TextDocument`.
- `IStreamableDocument` (src/OneWare.Essentials/ViewModels/IStreamableDocument.cs): enable live
  streaming to a document view.
- `IWaitForContent` (src/OneWare.Essentials/ViewModels/IWaitForContent.cs): lazy initialize content.
- `ICanHaveObservableItems` (src/OneWare.Essentials/ViewModels/ICanHaveObservableItems.cs):
  observable item collections.
- `INoSerializeLayout` (src/OneWare.Essentials/ViewModels/INoSerializeLayout.cs): opt out of layout
  persistence.

### Language and editor services

- `ILanguageService` (src/OneWare.Essentials/LanguageService/ILanguageService.cs): LSP-backed
  language service operations (completion, hover, formatting, etc.).
- `ITypeAssistance` (src/OneWare.Essentials/LanguageService/ITypeAssistance.cs): editor assistance
  for comments, formatting, and UI quick options.
- `IFoldingStrategy` / `IFormattingStrategy`
  (src/OneWare.Essentials/EditorExtensions/IFoldingStrategy.cs,
  src/OneWare.Essentials/EditorExtensions/IFormattingStrategy.cs): custom folding and formatting
  handlers.

## Universal FPGA Project System

The Universal FPGA Project System provides the project model, toolchain integration, and hardware
model for FPGA workflows. It is designed to be extended by plugins.

### Project model and files

- Project file extension: `.fpgaproj` (see `UniversalFpgaProjectRoot.ProjectFileExtension`).
- Project type ID: `UniversalFPGAProject` (see `UniversalFpgaProjectRoot.ProjectType`).
- Project files use JSON via `UniversalProjectProperties`. Keys are case-insensitive and stored in
  the project file. Common keys:
  - `include` / `exclude`: arrays of glob-like patterns used by `IsPathIncluded`.
  - `topEntity`: relative path to the top-level HDL file.
  - `toolchain`: toolchain ID to run on compile.
  - `loader`: loader ID to use for programming.
  - `fpga`: selected FPGA package name.
  - `testBenches`: list of relative paths flagged as test benches.
  - `compileExcluded`: list of relative paths excluded from compile.

`UniversalFpgaProjectRoot` also registers project entry modification handlers to update:
- Test bench overlays.
- Top entity overlays.
- Reduced opacity for compile-excluded files.

Project-specific files:
- `device-settings/<fpga>.deviceconf` stores board-specific settings
  (`FpgaSettingsParser` reads/writes a string map).
- `<testbench>.tbconf` stores test bench context (e.g. selected simulator).

### FpgaService registry

`FpgaService` is the central registry for FPGA extension points:

- `RegisterFpgaPackage(IFpgaPackage)` for hardware packages.
- `RegisterFpgaExtensionPackage(IFpgaExtensionPackage)` for board extensions.
- `RegisterToolchain<T>()`, `RegisterLoader<T>()`, `RegisterSimulator<T>()`.
- `RegisterTemplate<T>()` for project templates.
- `RegisterPreCompileStep<T>()` for pre-compile hooks.
- `RegisterNodeProvider<T>()` for HDL node extraction.
- `RegisterProjectPropertyMigration(...)` for project property migrations.
- `RegisterProjectEntryModification(Action<IProjectEntry>)` for custom project explorer adornments.

### Hardware packages

Hardware is loaded from `IPaths.PackagesDirectory/Hardware` with this structure:

```
Hardware/
  <PackageName>/
    FPGA/
      <FpgaName>/
        fpga.json
        gui.json (optional)
    Extensions/
      <ConnectorName>/
        <ExtensionName>/
          extension.json
          gui.json (optional)
```

You can also point to embedded assets using `avares://` URIs.

JSON structure for `fpga.json` (see `FpgaBase`):

```json
{
  "pins": [{ "name": "P1", "description": "CLK" }],
  "interfaces": [
    {
      "name": "UART",
      "connector": "PMOD",
      "pins": [{ "name": "TX", "pin": "P1" }]
    }
  ],
  "properties": { "VendorToolchain_Device": "XC7A35T" }
}
```

JSON structure for `extension.json` (see `FpgaExtensionBase`):

```json
{
  "pins": [{ "name": "J1", "description": "GPIO", "interfacePin": "TX" }],
  "interfaces": [
    {
      "name": "PMOD_OUT",
      "connector": "PMOD",
      "pins": [{ "name": "TX", "pin": "J1" }]
    }
  ]
}
```

### Toolchains, loaders, simulators, templates, nodes

Implement these interfaces to extend the toolchain pipeline:

- `IFpgaToolchain` (src/OneWare.UniversalFpgaProjectSystem/Services/IFpgaToolchain.cs):
  called by `FpgaService.RunToolchainAsync`. Typical responsibilities:
  - `OnProjectCreated`: set default project properties.
  - `LoadConnections` / `SaveConnections`: translate pin assignments to a toolchain format
    (example: `YosysToolchain` uses a PCF file).
  - `CompileAsync`: run synthesis/fit/assemble or orchestration.
- `IFpgaLoader` (src/OneWare.UniversalFpgaProjectSystem/Services/IFpgaLoader.cs):
  handles programming/download (example: `OpenFpgaLoader`).
- `IFpgaSimulator` (src/OneWare.UniversalFpgaProjectSystem/Services/IFpgaSimulator.cs):
  runs test benches and can provide UI with `TestBenchToolbarTopUiExtension`.
- `IFpgaProjectTemplate` (src/OneWare.UniversalFpgaProjectSystem/Services/IFpgaProjectTemplate.cs):
  fills a new project with starter files.
- `IFpgaPreCompileStep` (src/OneWare.UniversalFpgaProjectSystem/Services/IFpgaPreCompileStep.cs):
  optional steps executed before compile.
- `INodeProvider` (src/OneWare.UniversalFpgaProjectSystem/Services/INodeProvider.cs):
  extracts HDL nodes from an `IProjectFile`. The pin planner uses this to build
  connectable nodes for the top entity.

### Pin planner and connections

- The pin planner loads the selected FPGA (`IFpgaPackage.LoadFpga`) and runs the node provider on
  the top entity to build `FpgaNode` instances.
- Toolchains can persist connections in their own formats and must implement
  `LoadConnections` / `SaveConnections`.
- The selected FPGA name is stored in the project property `fpga`.

### UI extension hooks used by Universal FPGA tooling

Use `IWindowService.RegisterUiExtension` to add UI to these extension points:

- `MainWindow_RoundToolBarExtension`: main toolbar row.
- `UniversalFpgaToolBar_CompileMenuExtension`: compile button menu.
- `UniversalFpgaToolBar_PinPlannerMenuExtension`: pin planner menu.
- `UniversalFpgaToolBar_DownloaderConfigurationExtension`: download configuration area.
- `CompileWindow_TopRightExtension`: pin planner window (top right region).
- `EditView_Top`: top editor panel (used for test bench toolbar).

## Suggested validation and troubleshooting

- Ensure `compatibility.txt` matches the core dependency versions.
- Verify your module class is public and implements `IOneWareModule`.
- If your UI does not appear, confirm you used the correct UI extension key.
- For FPGA tooling, confirm your toolchain ID matches the project `toolchain` property.
