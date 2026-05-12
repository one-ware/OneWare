using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using OneWare.Essentials.ToolEngine;
using OneWare.ToolEngine.Services;
using Xunit;

namespace OneWare.ToolEngine.UnitTests.ToolEngine;

public class ToolCommandTests
{
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

            var normalizedInput = inputPath.Replace('\\', '/');
            var normalizedRoot = localProjectRoot.Replace('\\', '/');
            
            string fullPath;

            if (normalizedInput.StartsWith('/') || (normalizedInput.Length > 1 && normalizedInput[1] == ':'))
            {
                fullPath = normalizedInput;
            }
            else
            {
                fullPath = $"{normalizedRoot.TrimEnd('/')}/{normalizedInput.TrimStart('/')}";
            }

            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath;
        
            var relativePart = fullPath[normalizedRoot.Length..].TrimStart('/');

            return string.IsNullOrEmpty(relativePart)
                ? remoteWorkDir
                : $"{remoteWorkDir}/{relativePart}";

        }
    }
}