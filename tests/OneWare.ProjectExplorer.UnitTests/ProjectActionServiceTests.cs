using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.Essentials.Services;
using OneWare.ProjectExplorer.Services;
using Xunit;

namespace OneWare.ProjectExplorer.UnitTests;

public class ProjectActionServiceTests
{
    private readonly IProjectRoot _project = Proxy<IProjectRoot>((method, _) => method.Name switch
    {
        "get_FullPath" => "/tmp/project",
        "get_Header" => "project",
        _ => null
    });

    private readonly List<IProjectRoot> _savedProjects = [];

    private ProjectActionService CreateService(IProjectRoot? activeProject = null)
    {
        var explorer = Proxy<IProjectExplorerService>((method, args) => method.Name switch
        {
            "get_ActiveProject" => activeProject,
            "SaveOpenFilesForProjectAsync" => Save((IProjectRoot)args![0]!),
            _ => null
        });

        return new ProjectActionService(Proxy<ICompositeServiceProvider>(), explorer, NullLogger.Instance);
    }

    private Task<bool> Save(IProjectRoot project)
    {
        _savedProjects.Add(project);
        return Task.FromResult(true);
    }

    [Fact]
    public async Task ExecuteAsync_UsesHighestPrioritySupportingProvider()
    {
        var service = CreateService(_project);
        var fallback = new TestProvider("fallback", -100, ProjectActionKind.Debug);
        var specific = new TestProvider("specific", 0, ProjectActionKind.Debug);
        var unrelated = new TestProvider("unrelated", 100, ProjectActionKind.Build);
        service.RegisterProvider(fallback);
        service.RegisterProvider(specific);
        service.RegisterProvider(unrelated);

        var result = await service.ExecuteAsync(ProjectActionKind.Debug);

        Assert.True(result.Success);
        Assert.Equal(1, specific.Executions);
        Assert.Equal(0, fallback.Executions);
        Assert.Equal(0, unrelated.Executions);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutProvider_Fails()
    {
        var service = CreateService(_project);

        var result = await service.ExecuteAsync(ProjectActionKind.Run);

        Assert.False(result.Success);
        Assert.False(service.CanExecute(ProjectActionKind.Run));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutActiveProject_Fails()
    {
        var service = CreateService();
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Build));

        var result = await service.ExecuteAsync(ProjectActionKind.Build);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Build_StoresArtifacts_AndClean_RemovesThem()
    {
        var service = CreateService(_project);
        var artifact = new BuildArtifact(BuildArtifactKind.Executable, "/tmp/project/build/app");
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Build, ProjectActionKind.Clean)
        {
            Artifacts = [artifact]
        });

        await service.ExecuteAsync(ProjectActionKind.Build);
        Assert.Equal([artifact], service.GetArtifacts(_project));

        await service.ExecuteAsync(ProjectActionKind.Clean);
        Assert.Empty(service.GetArtifacts(_project));
    }

    [Fact]
    public async Task Build_SavesOpenFiles_ButProgramDoesNot()
    {
        var service = CreateService(_project);
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Build, ProjectActionKind.Program));

        await service.ExecuteAsync(ProjectActionKind.Program);
        Assert.Empty(_savedProjects);

        await service.ExecuteAsync(ProjectActionKind.Build);
        Assert.Equal([_project], _savedProjects);
    }

    [Fact]
    public async Task SecondTopLevelAction_IsRejected_WhileNestedActionRuns()
    {
        var service = CreateService(_project);
        var gate = new TaskCompletionSource();
        var build = new TestProvider("build", 0, ProjectActionKind.Build);
        var debug = new TestProvider("debug", 0, ProjectActionKind.Debug)
        {
            // Debug builds first, from inside its own action.
            OnExecute = async _ =>
            {
                var nested = await service.ExecuteAsync(ProjectActionKind.Build);
                Assert.True(nested.Success);
                await gate.Task;
            }
        };
        service.RegisterProvider(build);
        service.RegisterProvider(debug);

        var running = service.ExecuteAsync(ProjectActionKind.Debug);

        Assert.True(service.IsBusy);
        Assert.Equal(ProjectActionKind.Debug, service.CurrentAction?.Kind);

        var rejected = await service.ExecuteAsync(ProjectActionKind.Build);
        Assert.False(rejected.Success);

        gate.SetResult();
        Assert.True((await running).Success);
        Assert.False(service.IsBusy);
        Assert.Equal(1, build.Executions);
    }

    [Fact]
    public async Task Cancel_CancelsTheRunningAction()
    {
        var service = CreateService(_project);
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Build)
        {
            OnExecute = request => Task.Delay(Timeout.Infinite, request.CancellationToken)
        });

        var running = service.ExecuteAsync(ProjectActionKind.Build);
        service.Cancel();

        var result = await running;
        Assert.False(result.Success);
        Assert.False(service.IsBusy);
    }

    [Fact]
    public async Task ProviderException_IsReportedAsFailure()
    {
        var service = CreateService(_project);
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Run)
        {
            OnExecute = _ => throw new InvalidOperationException("boom")
        });

        var result = await service.ExecuteAsync(ProjectActionKind.Run);

        Assert.False(result.Success);
        Assert.Equal("boom", result.Message);
    }

    [Fact]
    public void RegisterProvider_IgnoresDuplicateIds()
    {
        var service = CreateService(_project);
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Run));
        service.RegisterProvider(new TestProvider("p", 0, ProjectActionKind.Build));

        Assert.Single(service.Providers);
    }

    private sealed class TestProvider(string id, int priority, params ProjectActionKind[] kinds)
        : IProjectActionProvider
    {
        public int Executions { get; private set; }
        public IReadOnlyList<BuildArtifact>? Artifacts { get; init; }
        public Func<ProjectActionRequest, Task>? OnExecute { get; init; }

        public string Id => id;
        public int Priority => priority;

        public bool Supports(IProjectRoot project, ProjectActionKind kind)
        {
            return kinds.Contains(kind);
        }

        public async Task<ProjectActionResult> ExecuteAsync(ProjectActionRequest request)
        {
            Executions++;
            if (OnExecute != null) await OnExecute(request);
            return ProjectActionResult.Succeeded(Artifacts);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?>? invoke = null) where T : class
    {
        var instance = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)instance).Handler = invoke;
        return instance;
    }

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Handler?.Invoke(targetMethod!, args);
        }
    }
}
