using System.Reflection;
using OneWare.Essentials.Models;
using OneWare.ProjectExplorer.ViewModels;
using Xunit;

namespace OneWare.ProjectExplorer.UnitTests;

public class ProjectViewModelBaseTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "oneware-tests", "design");

    [Fact]
    public void SameFolderCanBeLoadedAsDifferentProjectTypes()
    {
        var explorer = new TestProjectViewModel();
        var folderProject = Root<IProjectRoot>("Folder", Folder);
        var fpgaProject = Root<IProjectRootWithFile>("UniversalFPGAProject", Path.Combine(Folder, "design.fpgaproj"));

        explorer.AddProject(folderProject);
        explorer.AddProject(fpgaProject);

        Assert.Equal(2, explorer.Projects.Count);
        Assert.Same(folderProject, explorer.GetLoadedProject(Root<IProjectRoot>("Folder", Folder)));
        Assert.Same(fpgaProject,
            explorer.GetLoadedProject(Root<IProjectRootWithFile>("UniversalFPGAProject",
                Path.Combine(Folder, "design.fpgaproj"))));
    }

    [Fact]
    public void FileResolvesToTypedProjectWhenFolderIsLoadedTwice()
    {
        var explorer = new TestProjectViewModel();
        var folderProject = Root<IProjectRoot>("Folder", Folder);
        var fpgaProject = Root<IProjectRootWithFile>("UniversalFPGAProject", Path.Combine(Folder, "design.fpgaproj"));

        explorer.AddProject(folderProject);
        explorer.AddProject(fpgaProject);

        Assert.Same(fpgaProject, explorer.GetRootFromFile(Path.Combine(Folder, "src", "top.vhd")));
    }

    [Fact]
    public void FileResolvesToMostSpecificRootAndIgnoresSiblingPrefix()
    {
        var explorer = new TestProjectViewModel();
        var outer = Root<IProjectRoot>("Folder", Folder);
        var inner = Root<IProjectRoot>("Folder", Path.Combine(Folder, "sub"));

        explorer.AddProject(outer);
        explorer.AddProject(inner);

        Assert.Same(inner, explorer.GetRootFromFile(Path.Combine(Folder, "sub", "a.v")));
        Assert.Same(outer, explorer.GetRootFromFile(Path.Combine(Folder, "subfolder", "a.v")));
        Assert.Null(explorer.GetRootFromFile(Folder + "2" + Path.DirectorySeparatorChar + "a.v"));
    }

    private static T Root<T>(string typeId, string projectPath) where T : class, IProjectRoot
    {
        var rootFolder = typeof(T) == typeof(IProjectRoot) ? projectPath : Path.GetDirectoryName(projectPath)!;
        var instance = DispatchProxy.Create<T, InterfaceProxy>();
        ((InterfaceProxy)(object)instance).Handler = (method, _) => method.Name switch
        {
            "get_ProjectTypeId" => typeId,
            "get_ProjectPath" => projectPath,
            "get_RootFolderPath" or "get_FullPath" => rootFolder,
            "get_Header" => Path.GetFileName(rootFolder),
            "get_IsActive" or "IsPathIncluded" or "get_IsExpanded" => false,
            _ => method.ReturnType.IsValueType && method.ReturnType != typeof(void)
                ? Activator.CreateInstance(method.ReturnType)
                : null
        };
        return instance;
    }

    private sealed class TestProjectViewModel() : ProjectViewModelBase("Test");

    public class InterfaceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return Handler?.Invoke(targetMethod!, args);
        }
    }
}
