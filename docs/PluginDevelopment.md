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

### Auto-generate compatibility.txt from your csproj

You can generate `compatibility.txt` automatically during build by marking dependencies with
`Private="false"` and writing them into the output directory:

```msbuild
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <Version>1.0.0</Version>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <EnableDynamicLoading>true</EnableDynamicLoading>
        <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
        <SelfContained>false</SelfContained>
    </PropertyGroup>

    <ItemGroup>
        <AvaloniaResource Include="Assets\**\*.*" />
    </ItemGroup>
    
    <ItemGroup>
        <PackageReference Include="OneWare.Essentials" Version="1.0.0" Private="false" ExcludeAssets="runtime;Native" />
        <PackageReference Include="OneWare.UniversalFpgaProjectSystem" Version="1.0.0" Private="false" ExcludeAssets="runtime;Native" />
    </ItemGroup>

    <Target Name="GenerateCompatibilityFile" AfterTargets="Build">
        <ItemGroup>
            <FilteredDependencies Include="@(PackageReference)" Condition="'%(Private)' == 'false'" />
        </ItemGroup>

        <WriteLinesToFile
                File="$(OutDir)compatibility.txt"
                Lines="@(FilteredDependencies->'%(Identity) : %(Version)')"
                Overwrite="true" />
    </Target>
    
</Project>
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

The following sections document the core services and their key functions.

#### `IOneWareModule` (src/OneWare.Essentials/Services/IOneWareModule.cs)

- `Id`: module identifier (defaults to class name in `OneWareModuleBase`).
- `Dependencies`: other module IDs that must load before this one.
- `RegisterServices(IServiceCollection)`: add services to the DI container.
- `Initialize(IServiceProvider)`: run after services are registered.

#### `IPluginService` (src/OneWare.Essentials/Services/IPluginService.cs)

- `InstalledPlugins`: current plugin list.
- `AddPlugin(string path)`: loads a plugin folder and registers any modules.
- `RemovePlugin(IPlugin plugin)`: removes a plugin from the session.

#### `IPaths` (src/OneWare.Essentials/Services/IPaths.cs)

Provides all app path locations: `AppDataDirectory`, `ProjectsDirectory`, `PackagesDirectory`,
`PluginsDirectory`, `SettingsPath`, `TempDirectory`, and more. Use these instead of hard-coded paths.

#### `IWindowService` (src/OneWare.Essentials/Services/IWindowService.cs)

- `RegisterUiExtension(string key, OneWareUiExtension)`: add UI by key.
- `GetUiExtensions(string key)`: retrieve registered UI extensions.
- `RegisterMenuItem(string key, params MenuItemModel[])`: inject menu items by path.
- `GetMenuItems(string key)`: read menu entries for a path.
- `Show(...)`, `ShowDialogAsync(...)`: open windows/dialogs.
- `ShowMessageBoxAsync`, `ShowMessageAsync`, `ShowYesNoAsync`, `ShowYesNoCancelAsync`,
  `ShowProceedWarningAsync`, `ShowInputAsync`, `ShowFolderSelectAsync`, `ShowInputSelectAsync`:
  common dialogs and prompts.
- `ShowNotification(...)`, `ShowNotificationWithButton(...)`: toast notifications.
- `ActivateMainWindow()`: bring the main window to foreground.

#### `IMainDockService` (src/OneWare.Essentials/Services/IMainDockService.cs)

- `RegisterDocumentView<T>(extensions)`: map file extensions to document view models.
- `RegisterFileOpenOverwrite(action, extensions)`: override open behavior for extensions.
- `RegisterLayoutExtension<T>(location)`: add dockable layout extensions.
- `Show<T>()` / `Show(IDockable)`: display dockable views.
- `OpenFileAsync(fullPath)` / `CloseFileAsync(fullPath)`: open/close documents.
- `LoadLayout(name, reset)` / `SaveLayout()`: manage layout persistence.
- `SearchView(...)`: find a dockable in the layout.
- `InitializeContent()`: initialize dock content when needed.

#### `IProjectExplorerService` (src/OneWare.Essentials/Services/IProjectExplorerService.cs)

- `AddProject`, `TryCloseProjectAsync`, `ReloadProjectAsync`, `SaveProjectAsync`: project lifecycle.
- `LoadProjectFolderDialogAsync`, `LoadProjectFileDialogAsync`, `LoadProjectAsync`: load projects.
- `GetRootFromFile(filePath)`: map a file to its project root.
- `GetEntryFromFullPath(path)`: resolve or construct nodes for UI navigation.
- `RegisterConstructContextMenu(...)`: extend project explorer context menus.
- `ImportAsync(...)`: import files/folders into a project.
- `LoadRecentProjects()`, `OpenLastProjectsFileAsync()`: recent project helpers.
- Selection helpers: `ClearSelection`, `AddToSelection`.

#### `IProjectManagerService` (src/OneWare.Essentials/Services/IProjectManagerService.cs)

- `RegisterProjectManager(id, manager)`: register a project type.
- `GetManager(id)` / `GetManagerByExtension(extension)`: resolve project manager by ID/extension.

#### `IProjectSettingsService` (src/OneWare.Essentials/Services/IProjectSettingsService.cs)

- `AddProjectSetting`, `AddProjectSettingIfNotExists`: register project-specific settings.
- `GetProjectSettingsList(...)`, `GetProjectCategories()`: enumerate settings.
- `GetDefaultProjectCategory()`: default category name.

#### `ISettingsService` (src/OneWare.Essentials/Services/ISettingsService.cs)

- `RegisterSettingCategory`, `RegisterSettingSubCategory`: group settings.
- `Register<T>(key, defaultValue)`: register a plain setting.
- `RegisterSetting(...)`, `RegisterCustom(...)`: register richer settings.
- `GetSetting`, `HasSetting`, `GetSettingValue<T>`, `SetSettingValue(...)`: access values.
- `Bind<T>(key, observable)`, `GetSettingObservable<T>(key)`: reactive settings.
- `Load(path)`, `Save(path, autoSave)`, `Reset(key)`, `ResetAll()`: storage and reset.

#### `IApplicationCommandService` (src/OneWare.Essentials/Services/IApplicationCommandService.cs)

- `RegisterCommand(IApplicationCommand)`: add an application command.
- `LoadKeyConfiguration()`, `SaveKeyConfiguration()`: key binding persistence.

#### `ILanguageManager` (src/OneWare.Essentials/Services/ILanguageManager.cs)

- `RegisterTextMateLanguage(id, grammarPath, extensions)`: syntax highlighting.
- `RegisterLanguageExtensionLink(source, target)`: map extension to another language.
- `RegisterService(type, workspaceDependent, supportedFileTypes)`: LSP service registration.
- `RegisterStandaloneTypeAssistance(type, supportedFileTypes)`: editor assistance only.
- `GetLanguageService(fullPath)`, `GetTypeAssistance(IEditor)`: resolve per-file services.

#### `IErrorService` (src/OneWare.Essentials/Services/IErrorService.cs)

- `RegisterErrorSource(source)`: add a diagnostic source.
- `RefreshErrors(errors, source, filePath)`: replace diagnostics for a file.
- `Clear(source)`, `ClearFile(filePath)`: remove diagnostics.
- `GetErrors()`, `GetErrorsForFile(filePath)`: enumerate diagnostics.
- `ErrorRefresh` event: notify listeners to update.

#### `IOutputService` (src/OneWare.Essentials/Services/IOutputService.cs)

- `WriteLine(text, color, owner)`: append line with optional color/project owner.
- `Write(text, color, owner)`: append inline text.

#### `IFileIconService` (src/OneWare.Essentials/Services/IFileIconService.cs)

- `RegisterFileIcon(IObservable<IImage>, extensions)` / `RegisterFileIcon(resourceName, extensions)`.
- `GetFileIcon(extension)`: resolve `IImage` observable.
- `GetFileIconModel(extension)`: resolve `IconModel`.

#### `IChildProcessService` (src/OneWare.Essentials/Services/IChildProcessService.cs)

- `StartChildProcess(startInfo)`, `GetChildProcesses(path)`, `Kill(...)`: process control.
- `ExecuteShellAsync(path, arguments, workingDirectory, status, ...)`: run a tool with UI status.
- `StartWeakProcess(path, arguments, workingDirectory)`: launch and track a weak process.

#### `ITerminalManagerService` (src/OneWare.Essentials/Services/ITerminalManagerService.cs)

- `ExecuteInTerminalAsync(command, id, workingDirectory, showInUi, timeout, cancellationToken)`:
  run a command in a terminal pane and return the result.

#### `IToolService` (src/OneWare.Essentials/Services/IToolService.cs)

- `Register(toolContext, strategy)`: add a tool + default strategy.
- `RegisterStrategy(toolKey, strategy)`: add a strategy for an existing tool.
- `Unregister(toolContext)` / `Unregister(toolKey)` / `UnregisterStrategy(strategyKey)`: remove.
- `GetAllTools()`, `GetStrategies(toolKey)`, `GetStrategy(toolKey)`, `GetStrategyKeys(toolKey)`:
  query registered tools and strategies.

#### `IToolExecutionStrategy` (src/OneWare.Essentials/Services/IToolExecutionStrategy.cs)

- `ExecuteAsync(ToolCommand)`: run a tool.
- `GetStrategyName()`: display name.
- `GetStrategyKey()`: unique key.

#### `IToolExecutionDispatcherService` (src/OneWare.Essentials/Services/IToolExecutionDispatcherService.cs)

- `ExecuteAsync(ToolCommand)`: dispatch a tool command through configured strategy.

#### `IPackageService` (src/OneWare.Essentials/Services/IPackageService.cs)

- `RegisterPackage(Package)` / `RegisterPackageRepository(url)`: add packages/repositories.
- `RefreshAsync()`: refresh package metadata.
- `InstallAsync(...)`, `UpdateAsync(...)`, `RemoveAsync(packageId)`: package lifecycle.
- `CheckCompatibilityAsync(packageId, version)`: compatibility check.
- `DownloadLicenseAsync(package)`, `DownloadPackageIconAsync(package)`: metadata downloads.
- `PackagesUpdated`, `PackageProgress` events: UI refresh and progress.

#### `IPackageWindowService` (src/OneWare.Essentials/Services/IPackageWindowService.cs)

- `ShowExtensionManager(...)`, `ShowExtensionManagerAsync(packageId)`: show extension manager UI.
- `ShowExtensionManagerAndTryInstallAsync(packageId)`, `QuickInstallPackageAsync(packageId)`:
  open the UI and optionally install.

#### `IApplicationStateService` (src/OneWare.Essentials/Services/IApplicationStateService.cs)

- `AddState(status, state, terminate)`: add a running state/process.
- `RemoveState(process, finishMessage)`: mark a state as completed.
- `RegisterAutoLaunchAction`, `RegisterPathLaunchAction`, `RegisterUrlLaunchAction`:
  add custom launch handlers.
- `ExecuteAutoLaunchActions`, `ExecutePathLaunchActions`, `ExecuteUrlLaunchActions`:
  invoke handlers for a launch request.
- `RegisterShutdownAction`, `RegisterShutdownTask`: lifecycle hooks.
- `TryShutdownAsync()`, `TryRestartAsync()`: app lifecycle operations.
- Notification helpers: `AddNotification`, `ClearNotifications`.

#### `IEnvironmentService` (src/OneWare.Essentials/Services/IEnvironmentService.cs)

- `SetEnvironmentVariable(key, value)`: set environment variables for tool execution.
- `SetPath(key, path)`: modify PATH entries.

#### `IHttpService` (src/OneWare.Essentials/Services/IHttpService.cs)

- `DownloadFileAsync(url, stream/location, progress, timeout, cancellationToken)`.
- `DownloadAndExtractArchiveAsync(url, location, progress, timeout, cancellationToken)`.
- `DownloadImageAsync(url, timeout, cancellationToken)`, `DownloadTextAsync(url, timeout, cancellationToken)`.

#### `IChatService` (src/OneWare.Essentials/Services/IChatService.cs)

- `InitializeAsync()`: setup and authenticate.
- `SendAsync(prompt)`: send a prompt.
- `AbortAsync()`: cancel current request.
- `NewChatAsync()`: reset conversation state.
- Events: `SessionReset`, `EventReceived`, `StatusChanged` for UI updates.

#### `IChatManagerService` (src/OneWare.Essentials/Services/IChatManagerService.cs)

- `RegisterChatService(IChatService)`: add a chat provider.
- `SelectedChatService`: currently selected provider.
- `SaveState()`: persist selection.

#### `IAiFunctionProvider` (src/OneWare.Essentials/Services/IAiFunctionProvider.cs)

- `GetTools()`: register AI tools for chat or automation.
- `FunctionStarted`, `FunctionCompleted` events.

#### `ISerialMonitorService` (src/OneWare.Essentials/Services/ISerialMonitorService.cs)

Dockable serial monitor placeholder; no additional API surface today.

#### `IWelcomeScreenService` / `IWelcomeScreenItem`

- `RegisterItemToNew`, `RegisterItemToOpen`, `RegisterItemToWalkthrough`: add welcome screen items.
- `IWelcomeScreenItem` provides `Name`, `Icon`, and `Command` for UI rendering.

#### `ICompositeServiceProvider` (src/OneWare.Essentials/Services/ICompositeServiceProvider.cs)

Aggregates services from the app and plugins via `IServiceProvider` and `IServiceProviderIsService`.

### Services in practice

Common extension patterns used across the built-in modules:

- Add project explorer menu items using `IProjectExplorerService.RegisterConstructContextMenu`.
- Add main menu items using `IWindowService.RegisterMenuItem` with a key path such as
  `MainWindow_MainMenu/File/New`.
- Show tools or documents using `IMainDockService.Show<T>()` and `IMainDockService.OpenFileAsync`.
- Register settings for your module via `ISettingsService.RegisterSetting`.
- Attach file icons with `IFileIconService.RegisterFileIcon`.
- Emit build or tool output via `IOutputService.WriteLine` and report errors via `IErrorService`.


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

## Chatbot integration

To add your own chat service (for example, a Copilot-like integration), implement
`IChatService` and register it with `IChatManagerService`.

Key points:

- `IChatService.InitializeAsync` should set up any connections, models, or auth.
- Use `EventReceived` to stream chat content and `StatusChanged` to report activity or errors.
- Set `BottomUiExtension` to a custom `Avalonia.Controls.Control` if you need extra UI (model
  selector, provider settings, etc.).
- Call `IChatManagerService.RegisterChatService` during module initialization.

Skeleton example:

```csharp
using System.ComponentModel;
using Avalonia.Controls;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

public sealed class MyChatService : IChatService
{
    public string Name => "MyChat";
    public Control? BottomUiExtension => null;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SessionReset;
    public event EventHandler<ChatEvent>? EventReceived;
    public event EventHandler<StatusEvent>? StatusChanged;

    public Task<bool> InitializeAsync()
    {
        StatusChanged?.Invoke(this, new StatusEvent("Ready"));
        return Task.FromResult(true);
    }

    public Task SendAsync(string prompt)
    {
        EventReceived?.Invoke(this, new ChatEvent(prompt));
        return Task.CompletedTask;
    }

    public Task AbortAsync() => Task.CompletedTask;
    public Task NewChatAsync() { SessionReset?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class MyChatModule : OneWareModuleBase
{
    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IChatManagerService>()
            .RegisterChatService(new MyChatService());
    }
}
```
