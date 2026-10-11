using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using Xunit;

namespace OneWare.Studio.Cli.UnitTests;

public class ExtensionStoreCliModuleTests
{
    [Fact]
    public void RegisterCommands_RegistersExtensionStoreOperations()
    {
        using var services = CreateServiceProvider();
        var commands = new ExtensionStoreCliModule().RegisterCommands(services);

        var extensions = Assert.Single(commands);
        Assert.Equal("extensions", extensions.Name);
        Assert.Contains("extension", extensions.Aliases);
        Assert.Equal(
            ["search", "list", "install", "update", "uninstall"],
            extensions.Subcommands.Select(command => command.Name));
    }

    [Theory]
    [InlineData(new[] { "extensions", "update" }, true)]
    [InlineData(new[] { "extension", "uninstall", "sample" }, true)]
    [InlineData(new[] { "oneai", "train", "project.oneai", "extensions" }, false)]
    [InlineData(new[] { "studio", "start" }, false)]
    public void IsExtensionStoreCommand_IdentifiesStoreCommands(string[] args, bool expected)
    {
        Assert.Equal(expected, ExtensionStoreCliModule.IsExtensionStoreCommand(args));
    }

    [Theory]
    [InlineData("search")]
    [InlineData("update")]
    public void RegisterCommands_AllowsOptionalSearchAndUpdateIds(string subcommand)
    {
        using var services = CreateServiceProvider();
        var extensions = Assert.Single(new ExtensionStoreCliModule().RegisterCommands(services));
        var root = new RootCommand();
        root.Subcommands.Add(extensions);

        var result = root.Parse(["extensions", subcommand]);

        Assert.Empty(result.Errors);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection().BuildServiceProvider();
    }
}
