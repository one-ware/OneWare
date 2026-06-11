using System;
using System.Collections.Generic;
using System.Linq;
using OneWare.Essentials.Enums;
using OneWare.Essentials.ToolEngine;
using OneWare.ToolEngine.Services;
using Xunit;

namespace OneWare.ToolEngine.UnitTests.ToolEngine;

public class ToolCommandBuilderTests
{
    private const string DefaultTool = "gcc";

    [Fact]
    public void Build_MinimumSetup_ReturnsCorrectCommand()
    {
        var command = new ToolCommandBuilder(DefaultTool).Build();
        
        Assert.Equal(DefaultTool, command.ToolName);
        Assert.Equal(DefaultTool, command.Executable);
        Assert.Empty(command.CommandArguments);
        Assert.Equal(".", command.WorkingDirectory);
    }

    [Fact]
    public void Build_WithoutNameOrExecutable_ThrowsException()
    {
        var builder = new ToolCommandBuilder("");
        
        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void WithMethods_SetCorrectProperties()
    {
        var outputHandler = (string s) => true;
        var errorHandler = (string s) => false;
        
        var command = new ToolCommandBuilder(DefaultTool)
            .WithExecutable("/usr/bin/gcc")
            .WithWorkingDirectory("/temp")
            .WithStatus("Compiling...", AppState.Idle)
            .WithTimer(true)
            .WithOutputHandler(outputHandler)
            .WithErrorHandler(errorHandler)
            .Build();
        
        Assert.Equal("/usr/bin/gcc", command.Executable);
        Assert.Equal("/temp", command.WorkingDirectory);
        Assert.Equal("Compiling...", command.StatusMessage);
        Assert.Equal(AppState.Idle, command.State);
        Assert.True(command.ShowTimer);
        Assert.Equal(outputHandler, command.OutputHandler);
        Assert.Equal(errorHandler, command.ErrorHandler);
    }

    [Fact]
    public void AddMethods_StoreCorrectArgumentTypes()
    {
        var command = new ToolCommandBuilder(DefaultTool)
            .Add("-v")                             
            .AddPath("/src/main.c")                
            .AddOption("--level", "3")             
            .AddPathOption("-o", "/bin/out")       
            .Build();
        
        var args = command.CommandArguments.ToList();
        Assert.Equal(6, args.Count);
        Assert.IsType<CommandArgument>(args[0]);
        Assert.IsType<PathArgument>(args[1]);
        Assert.IsType<CommandArgument>(args[2]);
        Assert.IsType<CommandArgument>(args[3]);
        Assert.IsType<CommandArgument>(args[4]);
        Assert.IsType<PathArgument>(args[5]);
    }

    [Fact]
    public void ConditionalAdds_WorkCorrectly()
    {
        var command = new ToolCommandBuilder(DefaultTool)
            .AddIf(true, "--enabled")
            .AddIf(false, "--disabled")
            .AddIfNotNull("valid")
            .AddIfNotNull(null)
            .AddIfNotNull("  ")
            .Build();
        
        Assert.Equal(2, command.Arguments.Count);
        Assert.Contains("--enabled", command.Arguments);
        Assert.Contains("valid", command.Arguments);
        Assert.DoesNotContain("--disabled", command.Arguments);
    }

    [Fact]
    public void AddRawArguments_ParsesCorrectWithQuotes()
    {
        const string raw = "--opt1 \"C:\\Path With Spaces\\file.txt\" --opt2 simpleValue";
        
        var command = new ToolCommandBuilder(DefaultTool)
            .AddRawArguments(raw)
            .Build();
        
        var args = command.Arguments.ToList();
        Assert.Equal(4, args.Count);
        Assert.Equal("--opt1", args[0]);
        Assert.Equal(@"C:\Path With Spaces\file.txt", args[1]); 
        Assert.Equal("--opt2", args[2]);
        Assert.Equal("simpleValue", args[3]);
    }

    [Fact]
    public void AddScript_CreatesTemplateArgument()
    {
        var command = new ToolCommandBuilder(DefaultTool)
            .AddScript("echo PLACEHOLDER", ("PLACEHOLDER", "Hello"))
            .Build();
        
        var arg = Assert.Single(command.CommandArguments);
        Assert.IsType<TemplateArgument>(arg);
        Assert.Equal("echo Hello", command.Arguments.First());
    }

    [Fact]
    public void AddRangeAndAddPaths_WorkForEnumerables()
    {

        var lits = new List<string> { "a", "b" };
        var paths = new List<string> { "/p1", "/p2" };

        var command = new ToolCommandBuilder(DefaultTool)
            .AddRange(lits)
            .AddPaths(paths)
            .Build();
        
        Assert.Equal(4, command.Arguments.Count);
        Assert.IsType<CommandArgument>(command.CommandArguments.First());
        Assert.IsType<PathArgument>(command.CommandArguments.Last());
    }
}