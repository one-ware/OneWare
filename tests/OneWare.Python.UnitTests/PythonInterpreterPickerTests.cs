using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.Settings;
using Xunit;

namespace OneWare.Python.UnitTests;

public sealed class PythonInterpreterPickerTests
{
    [Fact]
    public async Task CancelLeavesWorkspaceSettingUnchanged()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var executable = fixture.AddExecutable("chosen/python");
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, executable);
        var before = fixture.Settings.GetSettingValue<Dictionary<string, string>>(
            PythonInterpreterService.WorkspaceInterpretersSettingKey);
        var picker = CreatePicker(fixture, (_, _) => Task.FromResult<object?>(null));

        await picker.SelectInterpreterAsync();

        Assert.Same(before, fixture.Settings.GetSettingValue<Dictionary<string, string>>(
            PythonInterpreterService.WorkspaceInterpretersSettingKey));
        Assert.Equal(executable, fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
    }

    [Fact]
    public async Task AutomaticRemovesWorkspaceOverride()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, fixture.AddExecutable("chosen/python"));
        var picker = CreatePicker(fixture, (_, arguments) => Task.FromResult<object?>(
            ((IEnumerable<object>)arguments![3]!).First()));

        await picker.SelectInterpreterAsync();

        Assert.Null(fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
    }

    [Fact]
    public async Task RefreshRediscoversCandidatesWithoutChangingSelection()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var calls = 0;
        var picker = CreatePicker(fixture, (_, arguments) =>
        {
            calls++;
            return Task.FromResult<object?>(calls == 1
                ? ((IEnumerable<object>)arguments![3]!).Single(x => Equals(x, "Refresh"))
                : null);
        });

        await picker.SelectInterpreterAsync();

        Assert.Equal(2, calls);
        Assert.Null(fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
    }

    [Theory]
    [InlineData(".py", true)]
    [InlineData(".pyi", true)]
    [InlineData(".txt", false)]
    public void ActivePythonDocumentUsesProjectRootOtherwiseActiveProject(string extension, bool usesDocument)
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var documentRoot = Path.Combine(fixture.Root, "document-project");
        var document = Proxy<IExtendedDocument>((method, _) => method.Name switch
        {
            "get_FullPath" => Path.Combine(documentRoot, "nested", "file" + extension),
            "get_Extension" => extension,
            _ => null
        });
        var dock = Proxy<IMainDockService>((method, _) => method.Name == "get_CurrentDocument" ? document : null);
        var projects = Proxy<IProjectExplorerService>((method, _) => method.Name switch
        {
            "get_ActiveProject" => Project(fixture.Workspace),
            "GetRootFromFile" => Project(documentRoot),
            _ => null
        });
        var picker = new PythonInterpreterPickerService(fixture.Service, Proxy<IWindowService>(), dock, projects,
            Proxy<IApplicationCommandService>(), fixture.Settings, Proxy<IPaths>());
        Assert.Equal(usesDocument ? documentRoot : fixture.Workspace, picker.GetActiveWorkspace());
    }

    [Fact]
    public void LoosePythonFileUsesContainingDirectory()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var directory = Path.Combine(fixture.Root, "loose-files");
        var document = Proxy<IExtendedDocument>((method, _) => method.Name switch
        {
            "get_FullPath" => Path.Combine(directory, "script.py"),
            "get_Extension" => ".py",
            _ => null
        });
        var dock = Proxy<IMainDockService>((method, _) => method.Name == "get_CurrentDocument" ? document : null);
        var picker = new PythonInterpreterPickerService(fixture.Service, Proxy<IWindowService>(), dock,
            Proxy<IProjectExplorerService>(), Proxy<IApplicationCommandService>(), fixture.Settings, Proxy<IPaths>());
        Assert.Equal(directory, picker.GetActiveWorkspace());
    }

    [Fact]
    public async Task NoWorkspaceExplainsGlobalSettingWithoutOpeningPicker()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        string? message = null;
        var windows = Proxy<IWindowService>((method, arguments) =>
        {
            Assert.Equal("ShowMessageAsync", method.Name);
            message = (string)arguments![1]!;
            return Task.CompletedTask;
        });
        var picker = new PythonInterpreterPickerService(fixture.Service, windows,
            Proxy<IMainDockService>(), Proxy<IProjectExplorerService>(), Proxy<IApplicationCommandService>(),
            fixture.Settings, Proxy<IPaths>());
        await picker.SelectInterpreterAsync();
        Assert.Contains("Settings", message);
    }

    [Fact]
    public void InitializeRegistersPaletteAndCodeMenuOnce()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var commandCount = 0;
        var menuCount = 0;
        var commands = Proxy<IApplicationCommandService>((method, args) =>
        {
            Assert.Equal("RegisterCommand", method.Name);
            Assert.Equal("Python: Select Interpreter", ((IApplicationCommand)args![0]!).Name);
            commandCount++;
            return null;
        });
        var windows = Proxy<IWindowService>((method, args) =>
        {
            Assert.Equal("RegisterMenuItem", method.Name);
            Assert.Equal("MainWindow_MainMenu/Code", args![0]);
            var menu = Assert.Single((MenuItemModel[])args[1]!);
            Assert.Equal("Python", menu.Header);
            Assert.Equal("Select Interpreter...", Assert.Single(menu.Items!).Header);
            menuCount++;
            return null;
        });
        var picker = new PythonInterpreterPickerService(fixture.Service, windows,
            Proxy<IMainDockService>(), Proxy<IProjectExplorerService>(), commands, fixture.Settings, Proxy<IPaths>());
        picker.Initialize();
        picker.Initialize();
        Assert.Equal(1, commandCount);
        Assert.Equal(1, menuCount);
    }

    private static PythonInterpreterPickerService CreatePicker(
        PythonInterpreterServiceTests.InterpreterFixture fixture,
        Func<MethodInfo, object?[]?, object?> select,
        ISettingsService? settings = null)
    {
        var windows = Proxy<IWindowService>((method, args) =>
            method.Name == "ShowInputSelectAsync" ? select(method, args) : Task.CompletedTask);
        var projects = Proxy<IProjectExplorerService>((method, _) =>
            method.Name == "get_ActiveProject" ? Project(fixture.Workspace) : null);
        return new PythonInterpreterPickerService(fixture.Service, windows,
            Proxy<IMainDockService>(), projects, Proxy<IApplicationCommandService>(),
            settings ?? Proxy<ISettingsService>(),
            Proxy<IPaths>((method, _) => method.Name == "get_SettingsPath"
                ? Path.Combine(fixture.Root, "settings.json")
                : null));
    }

    [Fact]
    public async Task ConfirmingMissingCurrentSelectionDoesNotResetToAutomatic()
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var missing = Path.Combine(fixture.Root, "missing-python");
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, missing);
        var picker = CreatePicker(fixture, (_, arguments) =>
        {
            var selected = Assert.IsType<PythonInterpreterCandidate>(arguments![4]);
            Assert.Contains("missing or invalid", selected.Label);
            return Task.FromResult<object?>(selected);
        });

        await picker.SelectInterpreterAsync();

        Assert.Equal(missing, fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfirmedSelectionsAreSavedImmediately(bool automatic)
    {
        using var fixture = new PythonInterpreterServiceTests.InterpreterFixture();
        var executable = fixture.AddExecutable("chosen/python");
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, Path.Combine(fixture.Root, "previous"));
        Directory.CreateDirectory(fixture.Root);
        try
        {
            var picker = CreatePicker(fixture, (_, arguments) =>
            {
                var options = (IEnumerable<object>)arguments![3]!;
                return Task.FromResult(automatic
                    ? options.First()
                    : options.OfType<PythonInterpreterCandidate>().Single(x => x.ExecutablePath == executable) as object);
            }, fixture.Settings);
            fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey, executable);

            await picker.SelectInterpreterAsync();

            var saved = new SettingsService();
            saved.Load(Path.Combine(fixture.Root, "settings.json"));
            using var service = new PythonInterpreterService(saved);
            Assert.Equal(automatic ? null : executable, service.GetWorkspaceInterpreter(fixture.Workspace));
        }
        finally
        {
            Directory.Delete(fixture.Root, true);
        }
    }

    private static IProjectRoot Project(string root) =>
        Proxy<IProjectRoot>((method, _) => method.Name == "get_RootFolderPath" ? root : null);

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?>? invoke = null) where T : class
    {
        var instance = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)instance).Handler = invoke;
        return instance;
    }

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler?.Invoke(targetMethod!, args);
    }
}
