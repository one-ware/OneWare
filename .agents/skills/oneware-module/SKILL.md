---
name: oneware-module
description: Creates or wires up a OneWare feature module (built-in src/OneWare.* project or external plugin) with the right csproj imports, IOneWareModule entry point, DI registration, solution entry and studio registration. Use when adding a new module/project, registering services or UI extensions from a module, moving a module between Core/Studio/Desktop, or scaffolding a plugin.
---

# OneWare modules

Every feature is an `IOneWareModule`. Plugins use the same contract, so built-in modules double as plugin
examples. The public API surface and extension points are documented in
[docs/PluginDevelopment.md](../../../docs/PluginDevelopment.md). Check it for the right service before adding a new one.

## Built-in module checklist

1. **Project**: `src/OneWare.<Name>/OneWare.<Name>.csproj`:

   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
       <Import Project="..\..\build\props\Base.props"/>
       <Import Project="..\..\build\props\OneWare.Module.props"/>
       <!-- Import one build/props/<Package>.props per package; don't add versioned PackageReferences inline -->

       <ItemGroup>
           <ProjectReference Include="..\OneWare.Essentials\OneWare.Essentials.csproj"/>
       </ItemGroup>
   </Project>
   ```

   `OneWare.Module.props` sets `net10.0`, nullable, implicit usings and compiled Avalonia bindings. For a
   new NuGet package, add `build/props/<Package>.props` (and list it under the `/build/props/` folder in `OneWare.slnx`).

2. **Entry point**: `<Name>Module.cs` in the project root:

   ```csharp
   using Microsoft.Extensions.DependencyInjection;
   using OneWare.Essentials.Services;

   namespace OneWare.<Name>;

   public class <Name>Module : OneWareModuleBase
   {
       public override void RegisterServices(IServiceCollection services)
       {
           services.AddSingleton<IMyService, MyService>();
       }

       public override void Initialize(IServiceProvider serviceProvider)
       {
           // Resolve host services and register UI/language/tool extensions here.
           serviceProvider.Resolve<ILanguageManager>()...;
       }
   }
   ```

   - `RegisterServices` only registers. Don't resolve services there.
   - `Initialize` runs after all modules registered their services. Override `Dependencies` (module IDs,
     default `GetType().Name`) when another module must initialize first.
   - Resolve with `serviceProvider.Resolve<T>()` (or `ContainerLocator.Current` outside the module).

3. **Solution**: add the project to the matching folder in `OneWare.slnx` (`/Coremodules/`, `/Extensions/...`).

4. **Registration**: pick the layer that should ship it, then add a `ProjectReference` to that project and a
   `moduleCatalog.AddModule<<Name>Module>()` call in its `ConfigureModuleCatalog`:

   | Ships in | File | Project to reference from |
   |---|---|---|
   | Every OneWare app (Studio, Demo, Browser) | `src/OneWare.Core/App.axaml.cs` | `src/OneWare.Core/OneWare.Core.csproj` |
   | Studio desktop + browser | `studio/OneWare.Studio/StudioApp.cs` | `studio/OneWare.Studio/OneWare.Studio.csproj` |
   | Studio desktop only (native tools, processes, terminals) | `studio/OneWare.Studio.Desktop/DesktopStudioApp.cs` | `studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj` |

   Browser (WASM) builds can't start processes or use native libraries, so keep such modules desktop-only.

5. **UI**: follow the `oneware-styling` skill for any `.axaml`.

6. **Tests** (when the module has logic): `tests/OneWare.<Name>.UnitTests/`, importing `build/props/XUnit.props`
   (xUnit v2). For UI tests, also import `build/props/Avalonia.Headless.XUnit.props`, add a `TestApp.cs` with
   `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` and use `[AvaloniaFact]` (see
   `tests/OneWare.Dock.HeadlessTests`). Add the test project to the `/Tests/` folder in `OneWare.slnx`.

## External plugin

- Reference `OneWare.Essentials` (and optionally `OneWare.UniversalFpgaProjectSystem`) as NuGet packages with
  `Private="false" ExcludeAssets="runtime;Native"` and set `<EnableDynamicLoading>true</EnableDynamicLoading>`.
- Ship a `compatibility.txt` (`AssemblyName : Version` per line) next to the assemblies. The csproj snippet in
  `docs/PluginDevelopment.md` generates it on build.
- `tests/OneWare.TestPlugin` is a minimal in-repo plugin for reference.

## Validation

```bash
dotnet build src/OneWare.<Name>/OneWare.<Name>.csproj -v minimal
dotnet build studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj -v minimal   # registration compiles
dotnet test tests/OneWare.<Name>.UnitTests/OneWare.<Name>.UnitTests.csproj -v minimal  # if tests exist
```
