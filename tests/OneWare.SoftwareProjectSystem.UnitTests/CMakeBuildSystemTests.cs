using OneWare.SoftwareProjectSystem.Build;
using Xunit;

namespace OneWare.SoftwareProjectSystem.UnitTests;

public sealed class CMakeBuildSystemTests : IDisposable
{
    private readonly string _build = Path.Combine(Path.GetTempPath(), "oneware-cmake-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_build)) Directory.Delete(_build, true);
    }

    [Fact]
    public void FindsExecutablesFromFileApiReply()
    {
        var reply = Path.Combine(_build, ".cmake", "api", "v1", "reply");
        Directory.CreateDirectory(reply);
        File.WriteAllText(Path.Combine(reply, "index-2024.json"),
            """{ "reply": { "codemodel-v2": { "jsonFile": "codemodel-v2-1.json" } } }""");
        File.WriteAllText(Path.Combine(reply, "codemodel-v2-1.json"),
            """
            { "configurations": [ { "targets": [
                { "name": "lib", "jsonFile": "target-lib.json" },
                { "name": "tool", "jsonFile": "target-tool.json" },
                { "name": "app", "jsonFile": "target-app.json" }
            ] } ] }
            """);
        File.WriteAllText(Path.Combine(reply, "target-lib.json"),
            """{ "name": "lib", "type": "STATIC_LIBRARY", "artifacts": [ { "path": "liblib.a" } ] }""");
        File.WriteAllText(Path.Combine(reply, "target-tool.json"),
            """{ "name": "tool", "type": "EXECUTABLE", "artifacts": [ { "path": "tool" } ] }""");
        File.WriteAllText(Path.Combine(reply, "target-app.json"),
            """{ "name": "app", "type": "EXECUTABLE", "artifacts": [ { "path": "bin/app" } ] }""");

        Assert.Equal(Path.Combine(_build, "tool"), CMakeBuildSystem.FindExecutable(_build, null));
        Assert.Equal(Path.Combine(_build, "bin/app"), CMakeBuildSystem.FindExecutable(_build, "app"));
        Assert.Equal(Path.Combine(_build, "tool"), CMakeBuildSystem.FindExecutable(_build, "missing"));
    }

    [Fact]
    public async Task ConfiguresAndBuildsWithCMake()
    {
        if (Essentials.Helpers.PlatformHelper.GetFullPath("cmake" + Essentials.Helpers.PlatformHelper.ExecutableExtension) == null ||
            Essentials.Helpers.PlatformHelper.GetFullPath("g++" + Essentials.Helpers.PlatformHelper.ExecutableExtension) is not { } gxx ||
            Essentials.Helpers.PlatformHelper.GetFullPath("gcc" + Essentials.Helpers.PlatformHelper.ExecutableExtension) is not { } gcc)
            return;

        var root = Path.Combine(_build, "project");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "CMakeLists.txt"),
            "cmake_minimum_required(VERSION 3.16)\nproject(demo CXX)\nadd_executable(demo main.cpp)\n");
        await File.WriteAllTextAsync(Path.Combine(root, "main.cpp"), "int main() { return 0; }\n");

        var result = await new CMakeBuildSystem().BuildAsync(new SoftwareBuildRequest
        {
            RootDirectory = root,
            BuildDirectory = Path.Combine(root, "build", "Debug"),
            CompileCommandsDirectory = Path.Combine(root, "build"),
            Kit = new Services.ToolchainKit("k", "K", gcc, gxx),
            Configuration = "Debug",
            OutputName = "demo",
            Output = _ => { }
        }, CancellationToken.None);

        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(result.Executable));
        Assert.Equal("demo", Path.GetFileNameWithoutExtension(result.Executable));
        Assert.True(File.Exists(Path.Combine(root, "build", "compile_commands.json")));
    }

    [Fact]
    public void ReturnsNullWithoutReply()
    {
        Assert.Null(CMakeBuildSystem.FindExecutable(_build, null));
    }

    [Fact]
    public async Task FailsWithoutCMakeLists()
    {
        Directory.CreateDirectory(_build);
        var result = await new CMakeBuildSystem(_ => null).BuildAsync(new SoftwareBuildRequest
        {
            RootDirectory = _build,
            BuildDirectory = Path.Combine(_build, "build", "Debug"),
            CompileCommandsDirectory = Path.Combine(_build, "build"),
            Kit = new Services.ToolchainKit("k", "K", "cc", "c++"),
            Configuration = "Debug",
            OutputName = "app",
            Output = _ => { }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("CMakeLists.txt", result.Message);
    }
}
