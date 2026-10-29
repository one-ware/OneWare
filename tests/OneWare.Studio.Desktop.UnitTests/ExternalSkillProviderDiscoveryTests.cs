using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OneWare.Core.ModuleLogic;
using OneWare.Essentials.Services;
using OneWare.Studio.Desktop.Cli;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class ExternalSkillProviderDiscoveryTests
{
    [Fact]
    public void AgentInitInstallsBuiltInCliSkillWithoutPlugins()
    {
        var root = Path.Combine(Path.GetTempPath(), "oneware-builtin-skill-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new TestPaths(root);
            var catalog = new OneWareCliModuleCatalog();
            var services = new ServiceCollection();
            var loader = new CliModuleLoader(new CliHostBuilderContext(services, paths, catalog,
                new OneWareCliModuleManager(catalog), new TestLogger()));
            loader.RegisterBuiltInCliModules(_ => Task.FromResult(0));
            var provider = Assert.Single(loader.GetExternalSkillProviders());
            Assert.IsType<StudioExternalSkillProvider>(provider.Provider);

            var home = Path.Combine(root, "home");
            var command = new RootCommand();
            using var serviceProvider = services.BuildServiceProvider();
            command.Subcommands.Add(Assert.Single(new AgentCliModule(loader, paths, home).RegisterCommands(serviceProvider)));
            Assert.Equal(0, command.Parse(["agent", "init"]).Invoke());

            var skill = Path.Combine(home, ".agents", "skills", "oneware-cli", "SKILL.md");
            Assert.Contains("oneware --help", File.ReadAllText(skill));
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "oneware-skills.json")));
            var entry = manifest.RootElement.GetProperty("Skills").GetProperty("oneware-cli");
            Assert.StartsWith("builtin:", entry.GetProperty("Source").GetString());
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("Version").GetString()));
            Assert.Equal(0, command.Parse(["agent", "init"]).Invoke());
            File.AppendAllText(skill, "\nLocal changes\n");
            Assert.Equal(1, command.Parse(["agent", "init"]).Invoke());
            Assert.EndsWith("Local changes\n", File.ReadAllText(skill));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void IncompatiblePluginMakesDiscoveryFailRatherThanReportAnIncompleteList()
    {
        var root = Path.Combine(Path.GetTempPath(), "oneware-provider-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new TestPaths(root);
            Directory.CreateDirectory(Path.Combine(paths.PluginsDirectory, "broken"));
            var catalog = new OneWareCliModuleCatalog();
            var loader = new CliModuleLoader(new CliHostBuilderContext(new ServiceCollection(), paths, catalog,
                new OneWareCliModuleManager(catalog), new TestLogger()));

            loader.LoadPluginCliModules();
            var error = Assert.Throws<InvalidOperationException>(() => loader.GetExternalSkillProviders());
            Assert.Contains("broken", error.Message);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DiscoversProviderWithoutCliModule()
    {
        var root = Path.Combine(Path.GetTempPath(), "oneware-provider-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new TestPaths(root);
            var plugin = Path.Combine(paths.PluginsDirectory, "sample");
            Directory.CreateDirectory(plugin);
            var skillRoot = Path.Combine(plugin, "ExternalSkills");
            var skill = Path.Combine(skillRoot, "example-skill");
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"),
                "---\nname: example-skill\ndescription: Example\n---\nInstructions\n");
            ExternalSkillTestProvider.SkillRoot = skillRoot;
            File.Copy(typeof(ExternalSkillTestProvider).Assembly.Location,
                Path.Combine(plugin, "TestPlugin.dll"));
            File.WriteAllText(Path.Combine(plugin, "compatibility.txt"), "UnavailableTestDependency: 1.0.0");
            var catalog = new OneWareCliModuleCatalog();
            var logger = new TestLogger();
            var context = new CliHostBuilderContext(new ServiceCollection(), paths, catalog,
                new OneWareCliModuleManager(catalog), logger);
            var loader = new CliModuleLoader(context);

            loader.LoadPluginCliModules();
            IReadOnlyList<ExternalSkillProviderSource> discovered;
            try { discovered = loader.GetExternalSkillProviders(); }
            catch (Exception ex) { throw new InvalidOperationException(logger.Messages, ex); }
            var provider = Assert.Single(discovered);

            Assert.IsType<ExternalSkillTestProvider>(provider.Provider);
            Assert.Equal(plugin, provider.PluginPath);
            Assert.Empty(catalog.Modules);

            var home = Path.Combine(root, "home");
            var synchronizer = new ExternalSkillSynchronizer(paths.DocumentsDirectory, paths.PluginsDirectory,
                paths.PackagesDirectory, paths.AppName, home, TextWriter.Null, TextWriter.Null);
            Assert.Equal(0, synchronizer.Synchronize(discovered));
            Assert.True(File.Exists(Path.Combine(home, ".agents", "skills", "example-skill", "SKILL.md")));
            var command = new RootCommand();
            using var services = context.Services.BuildServiceProvider();
            command.Subcommands.Add(Assert.Single(new AgentCliModule(loader, paths, home).RegisterCommands(services)));
            Assert.Equal(0, command.Parse(["agent", "init"]).Invoke());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class TestPaths(string root) : IPaths
    {
        public string AppName => "OneWare Studio";
        public string AppIconPath => "";
        public string AppFolderName => "OneWareStudio";
        public string AppDataDirectory => root;
        public string TempDirectory => root;
        public string SessionDirectory => root;
        public string LayoutDirectory => root;
        public string SettingsPath => Path.Combine(root, "settings.json");
        public string DocumentsDirectory => root;
        public string CrashReportsDirectory => root;
        public string ProjectsDirectory => root;
        public string PackagesDirectory => Path.Combine(root, "Packages");
        public string NativeToolsDirectory => root;
        public string OnnxRuntimesDirectory => root;
        public string PluginsDirectory => Path.Combine(PackagesDirectory, "Plugins");
        public string ChangelogUrl => "";
        public string UpdateInfoUrl => "";
    }

    private sealed class TestLogger : ILogger
    {
        public string Messages { get; private set; } = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages += formatter(state, exception) + " " + exception;
    }
}

public sealed class ExternalSkillTestProvider : IExternalAgentSkillProvider
{
    public static string SkillRoot { get; set; } = "";
    public IReadOnlyCollection<string> GetSkillDirectories() => [SkillRoot];
}
