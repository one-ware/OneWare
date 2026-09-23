using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OneWare.Settings;
using Xunit;

namespace OneWare.Python.UnitTests;

public sealed class PythonInterpreterServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolutionFollowsDocumentedPrecedence(bool windows)
    {
        using var fixture = new InterpreterFixture(windows);
        var workspaceOverride = fixture.AddExecutable("selected/python");
        var dotVenv = fixture.AddEnvironment(fixture.Workspace, ".venv");
        var venv = fixture.AddEnvironment(fixture.Workspace, "venv");
        var global = fixture.AddExecutable("global/python");
        var path = fixture.AddPathPython();
        fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey, global);
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, workspaceOverride);

        Assert.Equal(workspaceOverride, fixture.Service.GetInterpreter(fixture.Workspace).ExecutablePath);
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, null);
        Assert.Equal(dotVenv, fixture.Service.GetInterpreter(fixture.Workspace).ExecutablePath);
        fixture.Executables.Remove(dotVenv);
        Assert.Equal(venv, fixture.Service.GetInterpreter(fixture.Workspace).ExecutablePath);
        fixture.Executables.Remove(venv);
        Assert.Equal(global, fixture.Service.GetInterpreter(fixture.Workspace).ExecutablePath);
        fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey, "");
        Assert.Equal(path, fixture.Service.GetInterpreter(fixture.Workspace).ExecutablePath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidExplicitSelectionNeverFallsBack(bool workspaceSelection)
    {
        using var fixture = new InterpreterFixture();
        var invalid = Path.Combine(fixture.Root, "missing", "python");
        fixture.AddPathPython();
        fixture.AddExecutable("global/python");
        if (workspaceSelection)
        {
            fixture.AddEnvironment(fixture.Workspace, ".venv");
            fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, invalid);
        }
        else
            fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey, invalid);

        var resolution = fixture.Service.GetInterpreter(fixture.Workspace);
        Assert.Equal(PythonInterpreterStatus.InvalidExplicitSelection, resolution.Status);
        Assert.False(resolution.IsResolved);
        Assert.Equal(invalid, resolution.ExecutablePath);
        Assert.Equal(workspaceSelection
            ? PythonInterpreterSource.WorkspaceOverride
            : PythonInterpreterSource.GlobalDefault, resolution.Source);
        Assert.Contains("no fallback", resolution.Message);
    }

    [Theory]
    [InlineData("python")]
    [InlineData("relative/python")]
    [InlineData("invalid\0path")]
    public void ExplicitSelectionRequiresValidAbsoluteExecutablePath(string path)
    {
        using var fixture = new InterpreterFixture();
        fixture.Executables.Add(path);
        fixture.AddPathPython();
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, path);
        Assert.Equal(PythonInterpreterStatus.InvalidExplicitSelection,
            fixture.Service.GetInterpreter(fixture.Workspace).Status);
    }

    [Fact]
    public void MissingInterpreterIsActionableUnresolvedState()
    {
        using var fixture = new InterpreterFixture();
        var resolution = fixture.Service.GetInterpreter(fixture.Workspace);
        Assert.Equal(PythonInterpreterStatus.Missing, resolution.Status);
        Assert.Null(resolution.ExecutablePath);
        Assert.Contains("Install Python externally", resolution.Message);
    }

    [Theory]
    [InlineData(false, "python3", "python")]
    [InlineData(true, "python.exe", "python3.exe")]
    public void PathFallbackUsesPlatformSpecificNameOrder(bool windows, string preferred, string other)
    {
        using var fixture = new InterpreterFixture(windows);
        var firstDirectory = Path.Combine(fixture.Root, "bin1");
        var secondDirectory = Path.Combine(fixture.Root, "bin2");
        fixture.Environment["PATH"] = string.Join(windows ? ';' : ':', firstDirectory, secondDirectory);
        fixture.Executables.Add(Path.Combine(firstDirectory, other));
        var preferredPath = Path.Combine(secondDirectory, preferred);
        fixture.Executables.Add(preferredPath);
        var result = fixture.Service.GetInterpreter(fixture.Workspace);
        Assert.Equal(preferredPath, result.ExecutablePath);
        Assert.Equal(PythonInterpreterSource.Path, result.Source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActivatedEnvironmentsArePickerCandidatesNotAutomaticOverrides(bool windows)
    {
        using var fixture = new InterpreterFixture(windows);
        var virtualEnv = Path.Combine(fixture.Root, "active virtual env");
        var condaEnv = Path.Combine(fixture.Root, "active conda env");
        fixture.Environment["VIRTUAL_ENV"] = virtualEnv;
        fixture.Environment["CONDA_PREFIX"] = condaEnv;
        var virtualPython = PythonInterpreterService.GetEnvironmentInterpreterPath(virtualEnv, windows);
        var condaPython = windows
            ? Path.Combine(condaEnv, "python.exe")
            : Path.Combine(condaEnv, "bin", "python");
        fixture.Executables.UnionWith([virtualPython, condaPython]);

        Assert.Equal(PythonInterpreterStatus.Missing, fixture.Service.GetInterpreter(fixture.Workspace).Status);
        var candidates = await fixture.Service.DiscoverCandidatesAsync(fixture.Workspace);
        Assert.Contains(candidates, x => x.ExecutablePath == virtualPython && x.Label == "VIRTUAL_ENV");
        Assert.Contains(candidates, x => x.ExecutablePath == condaPython && x.Label == "CONDA_PREFIX");
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task CandidateDeduplicationUsesPlatformPathComparison(bool windows, int count)
    {
        using var fixture = new InterpreterFixture(windows);
        var path = fixture.AddExecutable("custom/python");
        var upper = path.ToUpperInvariant();
        fixture.Executables.Add(upper);
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, path);
        fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey, upper);
        Assert.Equal(count, (await fixture.Service.DiscoverCandidatesAsync(fixture.Workspace)).Count);
    }

    [Fact]
    public void WorkspaceMapUpdatesAreImmutableAndNormalizePaths()
    {
        using var fixture = new InterpreterFixture();
        var original = fixture.Settings.GetSettingValue<Dictionary<string, string>>(
            PythonInterpreterService.WorkspaceInterpretersSettingKey);
        var interpreter = fixture.AddExecutable("\u74b0\u5883 with spaces/python");
        var equivalentWorkspace = Path.Combine(fixture.Workspace, ".", "child", "..") + Path.DirectorySeparatorChar;
        fixture.Service.SetWorkspaceInterpreter(equivalentWorkspace, interpreter);
        var updated = fixture.Settings.GetSettingValue<Dictionary<string, string>>(
            PythonInterpreterService.WorkspaceInterpretersSettingKey);

        Assert.Empty(original);
        Assert.NotSame(original, updated);
        Assert.Equal(interpreter, updated[fixture.Workspace]);
        Assert.Equal(interpreter, fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, null);
        Assert.Single(updated);
        Assert.Null(fixture.Service.GetWorkspaceInterpreter(fixture.Workspace));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkspaceSelectionsRoundTripThroughNormalSettingsPersistence(bool loadBeforeRegistration)
    {
        using var fixture = new InterpreterFixture();
        Directory.CreateDirectory(fixture.Root);
        try
        {
            var interpreter = fixture.AddExecutable("\u74b0\u5883 with spaces/python");
            fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, interpreter);
            var settingsPath = Path.Combine(fixture.Root, "settings.json");
            fixture.Settings.Save(settingsPath);
            var reloadedSettings = new SettingsService();
            if (loadBeforeRegistration) reloadedSettings.Load(settingsPath);
            using var reloaded = new PythonInterpreterService(reloadedSettings, false,
                fixture.Executables.Contains, _ => null);
            reloaded.Initialize();
            if (!loadBeforeRegistration) reloadedSettings.Load(settingsPath);

            Assert.Equal(interpreter, reloaded.GetInterpreter(fixture.Workspace).ExecutablePath);
            Assert.Equal(PythonInterpreterSource.WorkspaceOverride, reloaded.GetInterpreter(fixture.Workspace).Source);
        }
        finally
        {
            Directory.Delete(fixture.Root, true);
        }
    }

    [Fact]
    public void WorkspaceChangeNotifiesOnlyThatTrackedWorkspace()
    {
        using var fixture = new InterpreterFixture();
        var secondWorkspace = Path.Combine(fixture.Root, "second");
        fixture.Service.GetInterpreter(fixture.Workspace);
        fixture.Service.GetInterpreter(secondWorkspace);
        var changes = new List<PythonInterpreterChangedEventArgs>();
        fixture.Service.InterpreterChanged += (_, args) => changes.Add(args);
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, fixture.AddExecutable("selected/python"));

        Assert.Equal(fixture.Workspace, Assert.Single(changes).Workspace);
        Assert.Equal(PythonInterpreterStatus.Missing, fixture.Service.GetInterpreter(secondWorkspace).Status);
    }

    [Fact]
    public void GlobalChangeNotifiesOnlyWorkspacesDependingOnGlobalDefault()
    {
        using var fixture = new InterpreterFixture();
        var workspaceWithVenv = Path.Combine(fixture.Root, "venv-workspace");
        var workspaceWithOverride = Path.Combine(fixture.Root, "override-workspace");
        fixture.AddEnvironment(workspaceWithVenv, ".venv");
        fixture.Service.SetWorkspaceInterpreter(workspaceWithOverride, fixture.AddExecutable("override/python"));
        fixture.Service.GetInterpreter(fixture.Workspace);
        fixture.Service.GetInterpreter(workspaceWithVenv);
        fixture.Service.GetInterpreter(workspaceWithOverride);
        var changes = new List<PythonInterpreterChangedEventArgs>();
        fixture.Service.InterpreterChanged += (_, args) => changes.Add(args);

        fixture.Settings.SetSettingValue(PythonInterpreterService.GlobalInterpreterSettingKey,
            fixture.AddExecutable("new-global/python"));

        Assert.Equal(fixture.Workspace, Assert.Single(changes).Workspace);
    }

    [Fact]
    public void RefreshReportsNewWorkspaceEnvironmentWithoutSettingsChanges()
    {
        using var fixture = new InterpreterFixture();
        fixture.Service.GetInterpreter(fixture.Workspace);
        var changes = new List<PythonInterpreterChangedEventArgs>();
        fixture.Service.InterpreterChanged += (_, args) => changes.Add(args);
        var venv = fixture.AddEnvironment(fixture.Workspace, ".venv");
        fixture.Service.Refresh(fixture.Workspace);
        fixture.Service.Refresh(fixture.Workspace);
        Assert.Equal(venv, Assert.Single(changes).Resolution.ExecutablePath);
    }

    [Fact]
    public void AutomaticResetNotifiesAndRestoresFallback()
    {
        using var fixture = new InterpreterFixture();
        var fallback = fixture.AddPathPython();
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, fixture.AddExecutable("override/python"));
        fixture.Service.GetInterpreter(fixture.Workspace);
        var changes = new List<PythonInterpreterChangedEventArgs>();
        fixture.Service.InterpreterChanged += (_, args) => changes.Add(args);
        fixture.Service.SetWorkspaceInterpreter(fixture.Workspace, null);
        Assert.Equal(fallback, Assert.Single(changes).Resolution.ExecutablePath);
    }

    [Fact]
    public void InitializeIsIdempotentAndHiddenMapIsNotAVisibleSetting()
    {
        using var fixture = new InterpreterFixture();
        fixture.Service.Initialize();
        Assert.IsType<OneWare.Essentials.Models.Setting>(
            fixture.Settings.GetSetting(PythonInterpreterService.WorkspaceInterpretersSettingKey));
        Assert.Single(fixture.Settings.SettingCategories["Languages"].SettingSubCategories["Python"].Settings);
    }

    [Fact]
    public async Task DiscoveryAndPathInspectionAreBounded()
    {
        using var fixture = new InterpreterFixture();
        fixture.Environment["PATH"] = string.Join(':', Enumerable.Range(0, 1000)
            .Select(x => Path.Combine(fixture.Root, $"bin{x}")));
        foreach (var directory in fixture.Environment["PATH"]!.Split(':'))
            fixture.Executables.Add(Path.Combine(directory, "python3"));
        Assert.Equal(32, (await fixture.Service.DiscoverCandidatesAsync(fixture.Workspace)).Count);

        fixture.Executables.Clear();
        fixture.Executables.Add(Path.Combine(fixture.Root, "bin999", "python3"));
        Assert.Equal(PythonInterpreterStatus.Missing, fixture.Service.GetInterpreter(fixture.Workspace).Status);
    }

    [Fact]
    public void RealFilesystemPreservesVenvSymlinksAndRejectsNonExecutableFiles()
    {
        if (OperatingSystem.IsWindows()) return;
        using var fixture = new InterpreterFixture();
        var basePython = Path.Combine(fixture.Root, "base-python");
        var venvPython = Path.Combine(fixture.Workspace, ".venv", "bin", "python");
        Directory.CreateDirectory(Path.GetDirectoryName(venvPython)!);
        try
        {
            File.WriteAllText(basePython, "#!/bin/sh\nexit 0\n");
            File.SetUnixFileMode(basePython, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.CreateSymbolicLink(venvPython, basePython);
            var settings = new SettingsService();
            using var service = new PythonInterpreterService(settings);
            Assert.Equal(venvPython, service.GetInterpreter(fixture.Workspace).ExecutablePath);
            File.SetUnixFileMode(basePython, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            service.SetWorkspaceInterpreter(fixture.Workspace, venvPython);
            Assert.Equal(PythonInterpreterStatus.InvalidExplicitSelection, service.GetInterpreter(fixture.Workspace).Status);
        }
        finally
        {
            Directory.Delete(fixture.Root, true);
        }
    }

    [Fact]
    public void CorruptWorkspaceValuesRemainExplicitInvalidAndAreLoggedOnce()
    {
        using var fixture = new InterpreterFixture();
        fixture.Settings.SetSettingValue(PythonInterpreterService.WorkspaceInterpretersSettingKey,
            new Dictionary<string, string>
            {
                [fixture.Workspace] = "",
                ["relative-workspace"] = "python"
            });
        var logger = new RecordingLogger();
        using var service = new PythonInterpreterService(fixture.Settings, false, _ => true, _ => null, logger);

        var result = service.GetInterpreter(fixture.Workspace);
        service.GetInterpreter(fixture.Workspace);

        Assert.Equal(PythonInterpreterStatus.InvalidExplicitSelection, result.Status);
        Assert.Equal(PythonInterpreterSource.WorkspaceOverride, result.Source);
        Assert.Equal(2, logger.WarningCount);
    }

    private sealed class RecordingLogger : ILogger<PythonInterpreterService>
    {
        public int WarningCount { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) WarningCount++;
        }
    }

    internal sealed class InterpreterFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Directory.GetCurrentDirectory(), $"python-interpreter-test-{Guid.NewGuid():N}");
        public string Workspace => Path.Combine(Root, "workspace with spaces \u65e5\u672c\u8a9e");
        public SettingsService Settings { get; } = new();
        public Dictionary<string, string?> Environment { get; } = new();
        public HashSet<string> Executables { get; }
        public PythonInterpreterService Service { get; }
        private readonly bool _windows;

        public InterpreterFixture(bool windows = false)
        {
            _windows = windows;
            Executables = new HashSet<string>(windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            Service = new PythonInterpreterService(Settings, windows, Executables.Contains,
                key => Environment.GetValueOrDefault(key));
            Service.Initialize();
        }

        public string AddExecutable(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Executables.Add(path);
            return path;
        }

        public string AddEnvironment(string workspace, string name)
        {
            var path = PythonInterpreterService.GetEnvironmentInterpreterPath(Path.Combine(workspace, name), _windows);
            Executables.Add(path);
            return path;
        }

        public string AddPathPython()
        {
            var directory = Path.Combine(Root, "path-bin");
            Environment["PATH"] = directory;
            var path = Path.Combine(directory, _windows ? "python.exe" : "python3");
            Executables.Add(path);
            return path;
        }

        public void Dispose() => Service.Dispose();
    }
}
