using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OneWare.Essentials.Enums;
using OneWare.Essentials.ToolEngine;
using Xunit;

namespace OneWare.Essentials.UnitTests.ToolEngine;

public class ToolCommandTests
{
    [Fact]
    public void FromShellParams_ShouldCorrectlyMapProperties()
    {
        const string path = "/usr/bin/gcc.exe";
        const string workingDir = "/home/user";
        var args = new[] { "-v", "--help" };
        
        var command = ToolCommand.FromShellParams(path, args, workingDir, "Testing...");
        
        Assert.Equal("gcc", command.ToolName); 
        Assert.Equal(path, command.Executable);
        Assert.Equal(workingDir, command.WorkingDirectory);
        Assert.Equal(2, command.CommandArguments.Count);
        Assert.All(command.CommandArguments, a => Assert.IsType<CommandArgument>(a));
    }

    [Fact]
    public void FromWeakParams_ShouldSetMinimumRequiredState()
    {
        const string path = @"C:\Tools\MyApp.exe";
        var args = new List<string> { "arg1" };
        
        var command = ToolCommand.FromWeakParams(path, args, ".");
        
        Assert.Equal("MyApp", command.ToolName);
        Assert.Equal(AppState.Loading, command.State); 
        Assert.Equal("Running tool...", command.StatusMessage); 
    }

    [Fact]
    public void PrepareCommand_ShouldInvokePrepareOnAllArguments()
    {
        var pathArg = new PathArgument("folder/file.txt");
        var tempArg = new TemplateArgument("echo T", ("T", "val", false));
        
        var command = new ToolCommand
        {
            ToolName = "Test",
            CommandArguments = new List<ICommandArgument> { pathArg, tempArg }
        };
        
        command.PrepareCommand(OSPlatform.Windows);
        
        var resultArgs = command.Arguments.ToList();
        Assert.Equal("folder\\file.txt", resultArgs[0]); 
        Assert.Equal("echo val", resultArgs[1]);       
    }

    [Fact]
    // When creating ShellParams, all arguments are treated as command arguments.
    // Paths cannot be recognized and therefore cannot be modified
    public void PrepareCommand_WithPathMapper_ShouldTransformArguments()
    {
        var command = ToolCommand.FromShellParams(
            "test",
            ["input.txt"], 
            ".", 
            "status");

        command.PrepareCommand(OSPlatform.Linux, Mapper);
        
        Assert.Equal("input.txt", command.Arguments.First());
        return;

        string Mapper(string s) => s.Replace(".txt", ".bak");
    }

    [Fact]
    public void ArgumentsProperty_ShouldReflectCurrentStateOfCommandArguments()
    {
        var arg = new PathArgument("a/b");
        var command = new ToolCommand
        {
            ToolName = "Test",
            CommandArguments = new List<ICommandArgument> { arg }
        };
        
        Assert.Equal("a/b", command.Arguments.First());
        
        command.PrepareCommand(OSPlatform.Windows);
        
        Assert.Equal("a\\b", command.Arguments.First());
    }
    
    /// <summary>
    /// Verifies that local host paths are correctly mapped to container/remote paths.
    /// </summary>
    [Fact]
    public void PrepareCommand_ChangePaths()
    {
        var command = new ToolCommandBuilder("test")
            .Add("-k")
            .AddIf(false, "notPresent")
            .AddIf(true, "Present")
            .AddPath(@"C:\Users\User\OneWareStudio\Projects\Example\Example.v")
            .AddPath("folder/file.txt")
            .WithWorkingDirectory("/home/user")
            .AddPathOption("-o", "folder2/file.txt")
            .AddPaths(["folder/file2.txt", "folder2/file3.txt"])
            .Build();
            
        
        const string localProjectRoot = @"C:\Users\User\OneWareStudio\Projects\Example";
        const string remoteWorkDir = "/home/user/Example";


        command.PrepareCommand(OSPlatform.Linux, ContainerMapper);
        
        var resultArgs = command.Arguments.ToList();
        Assert.Equal("-k", resultArgs[0]); 
        Assert.Equal("Present", resultArgs[1]);
        Assert.Equal("/home/user/Example/Example.v", resultArgs[2]);
        Assert.Equal("/home/user/Example/folder/file.txt", resultArgs[3]);
        Assert.Equal("-o", resultArgs[4]);
        Assert.Equal("/home/user/Example/folder2/file.txt", resultArgs[5]);
        Assert.Equal("/home/user/Example/folder/file2.txt", resultArgs[6]);
        Assert.Equal("/home/user/Example/folder2/file3.txt", resultArgs[7]);
        return;


        string ContainerMapper(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return inputPath;

            var fullPath = Path.IsPathRooted(inputPath)
                ? Path.GetFullPath(inputPath)
                : Path.GetFullPath(Path.Combine(localProjectRoot, inputPath));

            if (!fullPath.StartsWith(localProjectRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');
            
            var relativePart = fullPath[localProjectRoot.Length..]
                .Replace('\\', '/')
                .TrimStart('/');

            return string.IsNullOrEmpty(relativePart)
                ? remoteWorkDir
                : $"{remoteWorkDir}/{relativePart}";

        }
    }
}